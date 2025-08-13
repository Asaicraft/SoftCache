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
            /// Floats/doubles are read via unsafe bit reinterpretation.
            /// </summary>
            /// <param name="parameters">Immutable parameter pack.</param>
            /// <returns>A compact 16-bit hash.</returns>
            
            """);

        // Statements: uint h = 0u;
        var statements = new List<StatementSyntax>
        {
            ParseStatement("uint h = 0u;")
        };

        // Inner unchecked { ... }
        var innerStatements = new List<StatementSyntax>();

        for (var i = 0; i < extractedParameters.Length; i++)
        {
            var parameter = extractedParameters[i];
            var accessExpression = $"parameters.{parameter.Name}";
            var kind = Classify(parameter.Type as ITypeSymbol ?? throw new InvalidOperationException("ITypeSymbol expected"));

            switch (kind)
            {
                case Kind.SmallInt16:
                    innerStatements.Add(ParseStatement($"uint x{i} = (uint){accessExpression};"));
                    innerStatements.Add(ParseStatement($"h ^= x{i};"));
                    break;

                case Kind.Int32Like:
                    innerStatements.Add(ParseStatement($"uint x{i} = (uint){accessExpression};"));
                    innerStatements.Add(ParseStatement($"x{i} ^= x{i} >> 16;"));
                    innerStatements.Add(ParseStatement($"h ^= x{i};"));
                    break;

                case Kind.Int64Like:
                    innerStatements.Add(ParseStatement($"ulong l{i} = (ulong){accessExpression};"));
                    innerStatements.Add(ParseStatement($"uint x{i} = (uint)(l{i} ^ (l{i} >> 16) ^ (l{i} >> 32) ^ (l{i} >> 48));"));
                    innerStatements.Add(ParseStatement($"h ^= x{i};"));
                    break;

                case Kind.Float32:
                    // Cannot take the address of a property — store into a temporary local variable.
                    innerStatements.Add(ParseStatement($"float __f{i} = {accessExpression};"));
                    innerStatements.Add(ParseStatement($"uint x{i} = *(uint*)&__f{i};"));
                    innerStatements.Add(ParseStatement($"x{i} ^= x{i} >> 16;"));
                    innerStatements.Add(ParseStatement($"h ^= x{i};"));
                    break;

                case Kind.Float64:
                    innerStatements.Add(ParseStatement($"double __d{i} = {accessExpression};"));
                    innerStatements.Add(ParseStatement($"ulong l{i} = *(ulong*)&__d{i};"));
                    innerStatements.Add(ParseStatement($"uint x{i} = (uint)(l{i} ^ (l{i} >> 16) ^ (l{i} >> 32) ^ (l{i} >> 48));"));
                    innerStatements.Add(ParseStatement($"h ^= x{i};"));
                    break;

                case Kind.ObjectLike:
                default:
                    innerStatements.Add(ParseStatement($"object? o{i} = (object?){accessExpression};"));
                    innerStatements.Add(ParseStatement($"uint x{i} = o{i} is null ? 0u : (uint)global::System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o{i});"));
                    innerStatements.Add(ParseStatement($"x{i} ^= x{i} >> 16;"));
                    innerStatements.Add(ParseStatement($"h ^= x{i};"));
                    break;
            }
        }

        statements.Add(
            CheckedStatement(
                SyntaxKind.UncheckedStatement,
                Block(innerStatements)));

        var seedExpression = softCacheOptions.GenerateGlobalSeed
            ? $"(ushort)(h ^ {SeedIdentifier(softCacheOptions, fullyQualifiedType)}.Seed)"
            : "(ushort)h";

        statements.Add(ParseStatement($"return {seedExpression};"));

        var body = Block(statements);

        var methodDeclaration = MethodDeclaration(
                PredefinedType(Token(SyntaxKind.UShortKeyword)),
                Identifier("MakeSoftHash"))
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.UnsafeKeyword)))
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

    private enum Kind { SmallInt16, Int32Like, Int64Like, Float32, Float64, ObjectLike }

    private static Kind Classify(ITypeSymbol type)
    {
        // Nullable<T> → treat as object-like (simplified)
        if (type is INamedTypeSymbol namedType && namedType.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T)
        {
            return Kind.ObjectLike;
        }

        // Enum → use underlying type
        if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumNamedType && enumNamedType.EnumUnderlyingType is ITypeSymbol underlyingType)
        {
            return Classify(underlyingType);
        }

        switch (type.SpecialType)
        {
            // 8/16-bit — direct mix
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
                return Kind.SmallInt16;

            // 32-bit lane — fold halves
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Char:
            case SpecialType.System_Boolean:
                return Kind.Int32Like;

            // 64-bit lane — fold quarters
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                return Kind.Int64Like;

            case SpecialType.System_Single:
                return Kind.Float32;

            case SpecialType.System_Double:
                return Kind.Float64;

            default:
                return Kind.ObjectLike;
        }
    }
}