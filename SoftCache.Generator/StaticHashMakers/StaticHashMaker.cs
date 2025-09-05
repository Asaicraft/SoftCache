using SoftCache.Annotations;
using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator.StaticHashMakers;
internal static class StaticHashMaker
{
    public static IStaticHashMaker GetStaticHashMaker(SoftCacheOptions options)
    {
        return options.HashKind switch
        {
            SoftHashKind.XorFold => XorHashMaker.Instance,
            SoftHashKind.Custom => CustomSoftHashMaker.Instance,
            _ => XorHashMaker.Instance,
        };
    }
}
