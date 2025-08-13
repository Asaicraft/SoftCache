using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace SoftCache.Generator;

/// <summary>
/// Defines a generator service responsible for producing the implementation of 
/// <c>ISoftCacheable&lt;TParameters&gt;.GetParameters()</c>.
/// <para>
/// The generated method must return the strongly-typed immutable parameters 
/// structure used by the <c>SoftCache</c> to identify cache entries.
/// </para>
/// </summary>
public interface IGetParametersMaker
{
    /// <summary>
    /// Creates the syntax tree for a strongly-typed 
    /// <c>ISoftCacheable&lt;TParameters&gt;.GetParameters()</c> implementation 
    /// for the specified type.
    /// <para>
    /// The generated method should have the exact form:
    /// <code>
    /// global::SomeNamespace.SomeType.Parameters 
    /// global::SoftCache.ISoftCacheable&lt;global::SomeNamespace.SomeType.Parameters&gt;.GetParameters()
    /// {
    ///     // ... return new Parameters(...);
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// The <paramref name="extractedParameters"/> sequence contains the set of 
    /// public, cache-relevant members (excluding those marked with 
    /// <c>[IgnoreCache]</c>) in the order they should appear in the 
    /// <c>Parameters</c> constructor call.
    /// </para>
    /// </summary>
    /// <param name="typeSymbol">
    /// The Roslyn symbol representing the target type for which the 
    /// <c>GetParameters()</c> method should be generated.
    /// </param>
    /// <param name="extractedParameters">
    /// The list of extracted parameters describing the identity-defining members 
    /// of the target type.
    /// </param>
    /// <returns>
    /// A <see cref="MethodDeclarationSyntax"/> representing the complete 
    /// implementation of <c>GetParameters()</c>.
    /// </returns>
    public MethodDeclarationSyntax CreateGetParameters(ITypeSymbol typeSymbol, ImmutableArray<ExtractedParameter> extractedParameters);
}
