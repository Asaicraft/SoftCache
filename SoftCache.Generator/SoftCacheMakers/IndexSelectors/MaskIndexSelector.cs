using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SoftCache.Generator.SoftCacheMakers.IndexSelectors;

public sealed class MaskIndexSelector : IIndexSelector
{
    public static readonly MaskIndexSelector Instance = new();

    public IEnumerable<StatementSyntax> CreateIndexStatement(CacheGenContext context)
    {
        // var {IndexName} = (unchecked((int)hash) & CacheMask);
        var expr = $"var {context.IndexName} = (unchecked((int)hash) & {context.CacheMaskName});";
        yield return ParseStatement(expr);
    }
}