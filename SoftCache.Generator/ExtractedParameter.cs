using Microsoft.CodeAnalysis;

namespace SoftCache.Generator;

/// <summary>
/// Describes a single extracted parameter for SoftCache code generation.
/// </summary>
/// <param name="RawSymbol">The original Roslyn symbol (property symbol).</param>
/// <param name="Name">The name of the parameter.</param>
/// <param name="Type">The type symbol of the parameter.</param>
public readonly record struct ExtractedParameter(
    ISymbol RawSymbol,
    string Name,
    ISymbol Type);
