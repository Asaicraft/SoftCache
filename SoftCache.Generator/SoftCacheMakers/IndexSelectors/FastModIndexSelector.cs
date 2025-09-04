using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SoftCache.Generator.SoftCacheMakers.IndexSelectors;

public sealed class FastModIndexSelector : IIndexSelector
{
    public static readonly FastModIndexSelector Instance = new();

    public StatementSyntax CreateIndexStatement(CacheGenContext context)
    {
        var cacheSize = context.CacheSize;
        var m = (ulong.MaxValue / (uint)cacheSize) + 1UL;

        // #if TARGET_64BIT
        //   var idx = (int)(((((M * (ulong)hash) >> 32) + 1) * CacheSize) >> 32);
        // #else
        //   var idx = (int)((uint)hash % CacheSize);
        // #endif
        var src =
$@"#if TARGET_64BIT
var {context.IndexName} = (int)((((({m}UL * (ulong)hash) >> 32) + 1) * {cacheSize} >> 32));
#else
var {context.IndexName} = (int)((uint)hash % {cacheSize});
#endif
";
        return ParseStatement(src);
    }
}