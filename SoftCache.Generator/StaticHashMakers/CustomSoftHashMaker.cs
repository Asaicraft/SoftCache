using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SoftCache.Generator.StaticHashMakers;

public sealed class CustomSoftHashMaker : IStaticHashMaker
{
    public static readonly CustomSoftHashMaker Instance = new();

    public MethodDeclarationSyntax MakeStatic(
        SoftCacheOptions softCacheOptions,
        ITypeSymbol typeSymbol,
        ImmutableArray<ExtractedParameter> _)
    {
        var fqn = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var parametersTypeName = $"{fqn}.Parameters";

        var xml = ParseLeadingTrivia($$"""
            /// <summary>
            /// Custom soft-hash entry point. 
            /// Provide the implementation in your partial class.
            /// <para>
            /// The return value is a <b>32-bit hash</b>, where the <b>lowest 16 bits</b> 
            /// are considered the primary bucket signal and thus the most important.
            /// </para>
            /// </summary>
            /// <param name="parameters">Immutable parameter pack.</param>
            /// <returns>User-defined 32-bit hash (low 16 bits carry primary weight).</returns>
            
            """);

        // static partial uint MakeSoftHash(in global::<FQN>.Parameters parameters);
        var method = MethodDeclaration(
                PredefinedType(Token(SyntaxKind.UIntKeyword)),
                Identifier("MakeSoftHash"))
            .WithModifiers(TokenList(
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.PartialKeyword)))
            .WithParameterList(
                ParameterList(
                    SingletonSeparatedList(
                        Parameter(Identifier("parameters"))
                            .WithModifiers(TokenList(Token(SyntaxKind.InKeyword)))
                            .WithType(ParseTypeName(parametersTypeName)))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
            .WithLeadingTrivia(xml);

        return method;
    }
}