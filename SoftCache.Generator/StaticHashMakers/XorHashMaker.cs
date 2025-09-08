using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static SoftCache.Generator.SoftCacheGenerator;

namespace SoftCache.Generator.StaticHashMakers;

public sealed class XorHashMaker : IStaticHashMaker
{
    public static readonly XorHashMaker Instance = new();

    public MethodDeclarationSyntax MakeStatic(
        SoftCacheOptions softCacheOptions,
        ITypeSymbol typeSymbol,
        ImmutableArray<ExtractedParameter> extractedParameters)
    {
        var fullyQualifiedTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var parametersTypeName = $"{fullyQualifiedTypeName}.Parameters";

        var xmlDocumentation = ParseLeadingTrivia($$"""
            /// <summary>
            /// Computes a soft hash by XOR folding without multiplications:
            /// 32-bit values are XOR'ed as-is; 64-bit values are folded into two 32-bit halves (lo ^ hi).
            /// Floats/doubles are reinterpreted via Unsafe.As (no /unsafe).
            /// <para><b>Note:</b> the <b>low 16 bits</b> carry the primary signal and are the most important.</para>
            /// </summary>
            /// <param name="parameters">Immutable parameter pack.</param>
            /// <returns>32-bit hash (primary bucket signal is in the low 16 bits).</returns>

            """);

        var statements = new List<StatementSyntax>
        {
            ParseStatement("uint hash = 0u;")
        };

        var uncheckedBodyStatements = new List<StatementSyntax>();

        for (var parameterIndex = 0; parameterIndex < extractedParameters.Length; parameterIndex++)
        {
            var extractedParameter = extractedParameters[parameterIndex];
            var accessExpression = $"parameters.{extractedParameter.Name}";
            var parameterKind = Classify(extractedParameter.Type as ITypeSymbol ?? throw new InvalidOperationException("ITypeSymbol expected"));

            switch (parameterKind)
            {
                case Kind.SmallInt16:
                {
                    uncheckedBodyStatements.Add(ParseStatement($"var value{parameterIndex} = (uint){accessExpression};"));
                    uncheckedBodyStatements.Add(ParseStatement($"hash ^= value{parameterIndex};"));
                    break;
                }

                case Kind.Int32Like:
                {
                    uncheckedBodyStatements.Add(ParseStatement($"var value{parameterIndex} = (uint){accessExpression};"));
                    // No splitting into 16-bit halves
                    uncheckedBodyStatements.Add(ParseStatement($"hash ^= value{parameterIndex};"));
                    break;
                }

                case Kind.Int64Like:
                {
                    uncheckedBodyStatements.Add(ParseStatement($"var longValue{parameterIndex} = (ulong){accessExpression};"));
                    // Two halves only: lo ^ hi
                    uncheckedBodyStatements.Add(ParseStatement($"var value{parameterIndex} = (uint)longValue{parameterIndex} ^ (uint)(longValue{parameterIndex} >> 32);"));
                    uncheckedBodyStatements.Add(ParseStatement($"hash ^= value{parameterIndex};"));
                    break;
                }

                case Kind.Float32:
                {
                    uncheckedBodyStatements.Add(ParseStatement($"var floatValue{parameterIndex} = {accessExpression};"));
                    uncheckedBodyStatements.Add(ParseStatement($"var value{parameterIndex} = global::System.Runtime.CompilerServices.Unsafe.As<float, uint>(ref floatValue{parameterIndex});"));
                    // 32-bit — XOR as-is
                    uncheckedBodyStatements.Add(ParseStatement($"hash ^= value{parameterIndex};"));
                    break;
                }

                case Kind.Float64:
                {
                    uncheckedBodyStatements.Add(ParseStatement($"var doubleValue{parameterIndex} = {accessExpression};"));
                    uncheckedBodyStatements.Add(ParseStatement($"var longValue{parameterIndex} = global::System.Runtime.CompilerServices.Unsafe.As<double, ulong>(ref doubleValue{parameterIndex});"));
                    // Two halves: lo ^ hi
                    uncheckedBodyStatements.Add(ParseStatement($"var value{parameterIndex} = (uint)longValue{parameterIndex} ^ (uint)(longValue{parameterIndex} >> 32);"));
                    uncheckedBodyStatements.Add(ParseStatement($"hash ^= value{parameterIndex};"));
                    break;
                }

                case Kind.Bool:
                {
                    uncheckedBodyStatements.Add(ParseStatement($"var value{parameterIndex} = {accessExpression} ? 1u : 0u;"));
                    uncheckedBodyStatements.Add(ParseStatement($"hash ^= value{parameterIndex};"));
                    break;
                }

                case Kind.ObjectLike:
                default:
                {
                    uncheckedBodyStatements.Add(ParseStatement($"object? objectValue{parameterIndex} = (object?){accessExpression};"));
                    // RuntimeHelpers.GetHashCode -> int; cast to uint and XOR as-is
                    uncheckedBodyStatements.Add(ParseStatement($"var value{parameterIndex} = objectValue{parameterIndex} is null ? 0u : (uint)global::System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(objectValue{parameterIndex});"));
                    uncheckedBodyStatements.Add(ParseStatement($"hash ^= value{parameterIndex};"));
                    break;
                }
            }
        }

        // unchecked { ... } — avoid overflow checks during mixing
        statements.Add(CheckedStatement(SyntaxKind.UncheckedStatement, Block(uncheckedBodyStatements)));

        var seedExpression = softCacheOptions.GenerateGlobalSeed
            ? $"(uint)(hash ^ {SoftCacheGenerator.SeedIdentifier(softCacheOptions, fullyQualifiedTypeName)}.Seed)"
            : "hash";

        statements.Add(ParseStatement($"return {seedExpression};"));

        var body = Block(statements);

        var methodDeclaration = MethodDeclaration(
                PredefinedType(Token(SyntaxKind.UIntKeyword)),
                Identifier("MakeSoftHash"))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(
                ParameterList(
                    SingletonSeparatedList(
                        Parameter(Identifier("parameters"))
                            .WithModifiers(TokenList(Token(SyntaxKind.ScopedKeyword), Token(SyntaxKind.InKeyword)))
                            .WithType(ParseTypeName(parametersTypeName)))))
            .WithBody(body)
            .WithLeadingTrivia(xmlDocumentation);

        return methodDeclaration;
    }

    private enum Kind { Bool, SmallInt16, Int32Like, Int64Like, Float32, Float64, ObjectLike }

    private static Kind Classify(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol named && named.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T)
        {
            return Kind.ObjectLike;
        }

        if (typeSymbol.TypeKind == TypeKind.Enum &&
            typeSymbol is INamedTypeSymbol enumNamed &&
            enumNamed.EnumUnderlyingType is ITypeSymbol underlying)
        {
            return Classify(underlying);
        }

        return typeSymbol.SpecialType switch
        {
            SpecialType.System_SByte or SpecialType.System_Byte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 => Kind.SmallInt16,

            SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Char => Kind.Int32Like,

            SpecialType.System_Int64 or SpecialType.System_UInt64 => Kind.Int64Like,

            SpecialType.System_Single => Kind.Float32,
            SpecialType.System_Double => Kind.Float64,

            SpecialType.System_Boolean => Kind.Bool,

            _ => Kind.ObjectLike
        };
    }
}