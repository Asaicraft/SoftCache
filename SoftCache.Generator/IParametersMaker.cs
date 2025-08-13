using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace SoftCache.Generator;

public interface IParametersMaker
{
    /// <summary>
    /// Creates:
    /// <code>
    /// public readonly [ref] struct Parameters
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
    public StructDeclarationSyntax MakeParameters(ITypeSymbol typeSymbol,
        ImmutableArray<ExtractedParameter> extractedParameters);
}