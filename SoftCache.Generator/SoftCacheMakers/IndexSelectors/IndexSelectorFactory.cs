using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator.SoftCacheMakers.IndexSelectors;

public static class IndexSelectorFactory
{
    public static IIndexSelector Create(CacheGenContext context)
    {
        var isPow2 = context.CacheSize == (1 << context.Options.CacheBits);
        return isPow2 ? MaskIndexSelector.Instance : FastModIndexSelector.Instance;
    }
}