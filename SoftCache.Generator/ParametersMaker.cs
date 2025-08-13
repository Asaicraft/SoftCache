using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace SoftCache.Generator;

public sealed class ParametersMaker : IParametersMaker
{
    /// <summary>
    /// Creates:
    /// <code>
    /// public readonly ref struct Parameters
    /// {
    ///     public global::Ns.Type1 P1 { get; }
    ///     public global::Ns.Type2 P2 { get; }
    ///     ...
    ///     public Parameters(global::Ns.Type1 p1, global::Ns.Type2 p2, ...)
    ///     {
    ///         this.P1 = p1;
    ///         this.P2 = p2;
    ///         ...
    ///     }
    /// }
    /// </code>
    /// </summary>
    public StructDeclarationSyntax MakeParameters(
        ITypeSymbol typeSymbol,
        ImmutableArray<ExtractedParameter> extractedParameters)
    {
        // Struct name
        var structIdentifier = Identifier("Parameters");

        // public readonly ref struct
        var modifiers = TokenList(
            Token(SyntaxKind.PublicKeyword),
            Token(SyntaxKind.ReadOnlyKeyword),
            Token(SyntaxKind.RefKeyword));

        // Properties
        var properties = extractedParameters.Select(parameter =>
        {
            var typeSyntax = ParseTypeName(ToFullyQualifiedName(parameter.Type));
            var propertyDeclaration = PropertyDeclaration(typeSyntax, Identifier(parameter.Name))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithAccessorList(AccessorList(
                    List(new[]
                    {
                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                    })));
            return (MemberDeclarationSyntax)propertyDeclaration;
        }).ToArray();

        // Constructor parameters
        var constructorParameters = SeparatedList(
            extractedParameters
                .Select((parameter, index) =>
                    Parameter(Identifier(ToCamelCase(parameter.Name)))
                        .WithType(ParseTypeName(ToFullyQualifiedName(parameter.Type))))
                .Select((parameterSyntax, index) => (SyntaxNodeOrToken)parameterSyntax)
                .ZipWithCommas<ParameterSyntax>());

        // Constructor body assignments: this.Name = name;
        var constructorStatements = extractedParameters.Select(parameter =>
            (StatementSyntax)ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ThisExpression(),
                        IdentifierName(parameter.Name)),
                    IdentifierName(ToCamelCase(parameter.Name))))).ToArray();

        var constructorDeclaration = ConstructorDeclaration(structIdentifier)
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(ParameterList(constructorParameters))
            .WithBody(Block(constructorStatements));

        // Build struct
        var parametersStruct = StructDeclaration(structIdentifier)
            .WithModifiers(modifiers)
            .WithMembers(List(properties.Concat([constructorDeclaration])));

        return parametersStruct;
    }

    private static string ToFullyQualifiedName(ISymbol typeSymbol)
    {
        // Prefer ITypeSymbol to get proper display; fall back to ISymbol
        if (typeSymbol is ITypeSymbol type)
        {
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        if (name.Length == 1)
        {
            return name.ToLowerInvariant();
        }

        if (char.IsUpper(name[0]) && (name.Length == 1 || !char.IsUpper(name[1])))
        {
            return char.ToLowerInvariant(name[0]) + name[1..];
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}

internal static class LinqHelpers
{
    public static SeparatedSyntaxList<T> ZipWithCommas<T>(this IEnumerable<SyntaxNodeOrToken> sequence)
        where T : SyntaxNode
    {
        var list = new List<SyntaxNodeOrToken>();
        var isFirst = true;

        foreach (var nodeOrToken in sequence)
        {
            if (!isFirst)
            {
                list.Add(Token(SyntaxKind.CommaToken));
            }

            list.Add(nodeOrToken);
            isFirst = false;
        }

        return SeparatedList<T>(list);
    }
}
