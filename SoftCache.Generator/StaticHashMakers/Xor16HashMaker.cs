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

public sealed class Xor16HashMaker : IStaticHashMaker
{
    public static readonly Xor16HashMaker Instance = new();

    public MethodDeclarationSyntax MakeStatic(
        SoftCacheOptions softCacheOptions,
        ITypeSymbol typeSymbol,
        ImmutableArray<ExtractedParameter> extractedParameters)
    {
        var fullyQualifiedType = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var parametersTypeName = $"{fullyQualifiedType}.Parameters";

        var xmlDocumentation = ParseLeadingTrivia($$"""
            /// <summary>
            /// Computes a 16-bit soft hash using per-type folding:
            /// 64-bit → XOR of four 16-bit quarters; 32-bit → XOR of two 16-bit halves.
            /// Floats/doubles are read via bit reinterpretation with Unsafe.As (no /unsafe).
            /// </summary>
            /// <param name="parameters">Immutable parameter pack.</param>
            /// <returns>A compact 16-bit hash.</returns>
            
            """);

        var statements = new List<StatementSyntax>
        {
            ParseStatement("uint h = 0u;")
        };

        var innerStatements = new List<StatementSyntax>();

        for (var i = 0; i < extractedParameters.Length; i++)
        {
            var extractedParameter = extractedParameters[i];
            var accessExpression = $"parameters.{extractedParameter.Name}";
            var kind = Classify(extractedParameter.Type as ITypeSymbol ?? throw new InvalidOperationException("ITypeSymbol expected"));

            switch (kind)
            {
                case Kind.SmallInt16:
                {
                    innerStatements.Add(ParseStatement($"uint x{i} = (uint){accessExpression};"));
                    innerStatements.Add(ParseStatement($"h ^= x{i};"));
                    break;
                }

                case Kind.Int32Like:
                {
                    innerStatements.Add(ParseStatement($"uint x{i} = (uint){accessExpression};"));
                    innerStatements.Add(ParseStatement($"x{i} ^= x{i} >> 16;"));
                    innerStatements.Add(ParseStatement($"h ^= x{i};"));
                    break;
                }

                case Kind.Int64Like:
                {
                    innerStatements.Add(ParseStatement($"ulong l{i} = (ulong){accessExpression};"));
                    innerStatements.Add(ParseStatement($"uint x{i} = (uint)(l{i} ^ (l{i} >> 16) ^ (l{i} >> 32) ^ (l{i} >> 48));"));
                    innerStatements.Add(ParseStatement($"h ^= x{i};"));
                    break;
                }

                case Kind.Float32:
                {
                    // No /unsafe: bit reinterpretation via Unsafe.As<float, uint>(ref value)
                    innerStatements.Add(ParseStatement($"float __f{i} = {accessExpression};"));
                    innerStatements.Add(ParseStatement($"uint x{i} = global::System.Runtime.CompilerServices.Unsafe.As<float, uint>(ref __f{i});"));
                    innerStatements.Add(ParseStatement($"x{i} ^= x{i} >> 16;"));
                    innerStatements.Add(ParseStatement($"h ^= x{i};"));
                    break;
                }

                case Kind.Float64:
                {
                    // No /unsafe: bit reinterpretation via Unsafe.As<double, ulong>(ref value)
                    innerStatements.Add(ParseStatement($"double __d{i} = {accessExpression};"));
                    innerStatements.Add(ParseStatement($"ulong l{i} = global::System.Runtime.CompilerServices.Unsafe.As<double, ulong>(ref __d{i});"));
                    innerStatements.Add(ParseStatement($"uint x{i} = (uint)(l{i} ^ (l{i} >> 16) ^ (l{i} >> 32) ^ (l{i} >> 48));"));
                    innerStatements.Add(ParseStatement($"h ^= x{i};"));
                    break;
                }

                case Kind.Bool:
                {
                    // No /unsafe: bool is treated as a byte (0 or 1)
                    innerStatements.Add(ParseStatement($"uint x{i} = {accessExpression} ? 1u : 0u;"));
                    innerStatements.Add(ParseStatement($"h ^= x{i};"));
                    break;
                }

                case Kind.ObjectLike:
                default:
                {
                    innerStatements.Add(ParseStatement($"object? o{i} = (object?){accessExpression};"));
                    innerStatements.Add(ParseStatement($"uint x{i} = o{i} is null ? 0u : (uint)global::System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o{i});"));
                    innerStatements.Add(ParseStatement($"x{i} ^= x{i} >> 16;"));
                    innerStatements.Add(ParseStatement($"h ^= x{i};"));
                    break;
                }
            }
        }

        statements.Add(
            CheckedStatement(
                SyntaxKind.UncheckedStatement,
                Block(innerStatements)));

        var seedExpression = softCacheOptions.GenerateGlobalSeed
            ? $"(ushort)(h ^ {SoftCacheGenerator.SeedIdentifier(softCacheOptions, fullyQualifiedType)}.Seed)"
            : "(ushort)h";

        statements.Add(ParseStatement($"return {seedExpression};"));

        var body = Block(statements);

        var methodDeclaration = MethodDeclaration(
                PredefinedType(Token(SyntaxKind.UShortKeyword)),
                Identifier("MakeSoftHash"))
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword))) // no /unsafe
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

        if (typeSymbol.TypeKind == TypeKind.Enum && typeSymbol is INamedTypeSymbol enumNamed && enumNamed.EnumUnderlyingType is ITypeSymbol underlying)
        {
            return Classify(underlying);
        }

        return typeSymbol.SpecialType switch
        {
            SpecialType.System_SByte or SpecialType.System_Byte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 => Kind.SmallInt16,

            SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Char  => Kind.Int32Like,

            SpecialType.System_Int64 or SpecialType.System_UInt64 => Kind.Int64Like,
            
            SpecialType.System_Single => Kind.Float32,
            SpecialType.System_Double => Kind.Float64,

            SpecialType.System_Boolean => Kind.Bool,

            _ => Kind.ObjectLike
        };
    }
}