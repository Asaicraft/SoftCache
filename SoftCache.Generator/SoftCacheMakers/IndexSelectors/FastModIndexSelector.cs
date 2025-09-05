using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SoftCache.Generator.SoftCacheMakers.IndexSelectors;

public sealed class FastModIndexSelector : IIndexSelector
{
    public static readonly FastModIndexSelector Instance = new();

    public IEnumerable<StatementSyntax> CreateIndexStatement(CacheGenContext cacheGenerationContext)
    {
        var cacheSize = cacheGenerationContext.CacheSize;
        var multiplier = (ulong.MaxValue / (uint)cacheSize) + 1UL;

        // 64-bit path:
        // - add #if TARGET_64BIT as leading trivia
        // - add ElasticCarriageReturnLineFeed to allow formatter to place newlines correctly
        // - keep "var ..." unchanged (important for trivia)
        var index64Statement = ParseStatement(
                $"var {cacheGenerationContext.IndexName} = (int)((((({multiplier}UL * (ulong)hash) >> 32) + 1) * {cacheSize} >> 32));")
            .WithLeadingTrivia(
                ParseLeadingTrivia("#if TARGET_64BIT").Add(ElasticCarriageReturnLineFeed))
            .WithTrailingTrivia(
                ElasticCarriageReturnLineFeed);

        // 32-bit path:
        // - add #else as leading trivia
        // - add #endif as trailing trivia
        // - add ElasticCarriageReturnLineFeed for formatting
        var index32Statement = ParseStatement(
                $"var {cacheGenerationContext.IndexName} = (int)((uint)hash % {cacheSize});")
            .WithLeadingTrivia(
                ParseLeadingTrivia("#else").Add(ElasticCarriageReturnLineFeed)
                )
            .WithTrailingTrivia(
                ParseTrailingTrivia("#endif").Add(ElasticCarriageReturnLineFeed));

        yield return index64Statement;
        yield return index32Statement;
    }
}