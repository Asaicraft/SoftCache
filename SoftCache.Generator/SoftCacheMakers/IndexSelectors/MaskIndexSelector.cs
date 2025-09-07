using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SoftCache.Generator.SoftCacheMakers.IndexSelectors;

public sealed class MaskIndexSelector : IIndexSelector
{
    public static readonly MaskIndexSelector Instance = new();

    public IEnumerable<StatementSyntax> CreateIndexStatement(CacheGenContext context)
    {
        // var {IndexName} = hash & CacheMask;
        var expr = $"var {context.IndexName} = hash & {context.CacheMaskName};";
        yield return ParseStatement(expr);
    }
}