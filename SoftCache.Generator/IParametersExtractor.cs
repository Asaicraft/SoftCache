using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace SoftCache.Generator;

public interface IParametersExtractor
{
    /// <summary>
    /// Extracts cache-relevant parameters from a type symbol.
    /// Only public, readable instance properties are considered.
    /// Properties marked with <c>IgnoreCache</c> are skipped.
    /// </summary>
    public ImmutableArray<ExtractedParameter> ExtractParameters(ITypeSymbol typeSymbol);
}