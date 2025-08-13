using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace SoftCache.Generator;

public sealed class GetParametersMaker : IGetParametersMaker
{
    /// <summary>
    /// Creates an explicit interface implementation for:
    /// <code>
    /// global::SomeNs.SomeType.Parameters 
    /// global::SoftCache.ISoftCacheable&lt;global::SomeNs.SomeType.Parameters&gt;.GetParameters()
    /// {
    ///     return new global::SomeNs.SomeType.Parameters(this.F1, this.F2, ...);
    /// }
    /// </code>
    /// </summary>
    public MethodDeclarationSyntax CreateGetParameters(
        ITypeSymbol typeSymbol,
        ImmutableArray<ExtractedParameter> extractedParameters)
    {
        // global::SomeNs.SomeType
        var fullyQualifiedType = ParseFullyQualifiedName(typeSymbol);

        // global::SomeNs.SomeType.Parameters
        var parametersType = SyntaxFactory.QualifiedName(
            (NameSyntax)fullyQualifiedType,
            SyntaxFactory.IdentifierName("Parameters"));

        // global::SoftCache
        var softCacheNamespace = SyntaxFactory.AliasQualifiedName(
            SyntaxFactory.IdentifierName(SyntaxFactory.Token(SyntaxKind.GlobalKeyword)),
            SyntaxFactory.IdentifierName("SoftCache"));

        // global::SoftCache.ISoftCacheable<global::SomeNs.SomeType.Parameters>
        var softCacheInterface = SyntaxFactory.QualifiedName(
            softCacheNamespace,
            SyntaxFactory.GenericName("ISoftCacheable")
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList<TypeSyntax>(parametersType))));

        // Explicit interface specifier: global::SoftCache.ISoftCacheable<...>.
        var explicitInterface = SyntaxFactory.ExplicitInterfaceSpecifier(softCacheInterface);

        // Arguments: this.PropertyName / this.FieldName (based on extracted parameters)
        var arguments = SyntaxFactory.SeparatedList(
            extractedParameters.Select(parameter =>
                SyntaxFactory.Argument(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ThisExpression(),
                        SyntaxFactory.IdentifierName(parameter.Name)))));

        // return new global::SomeNs.SomeType.Parameters(...);
        var returnStatement = SyntaxFactory.ReturnStatement(
            SyntaxFactory.ObjectCreationExpression(parametersType)
                .WithArgumentList(SyntaxFactory.ArgumentList(arguments)));

        // Method:
        // global::<type>.Parameters global::SoftCache.ISoftCacheable<global::<type>.Parameters>.GetParameters() { return new ...; }
        var methodDeclaration = SyntaxFactory.MethodDeclaration(parametersType, "GetParameters")
            .WithExplicitInterfaceSpecifier(explicitInterface)
            .WithBody(SyntaxFactory.Block(returnStatement));

        return methodDeclaration;
    }

    /// <summary>
    /// Parses fully qualified name for a symbol into a <see cref="NameSyntax"/>, e.g.
    /// "global::Ns.Outer.Inner`1" → <see cref="NameSyntax"/> with alias "global".
    /// </summary>
    private static NameSyntax ParseFullyQualifiedName(ISymbol symbol)
        => (NameSyntax)SyntaxFactory.ParseName(
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
}