using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator.SoftCacheMaker;
public sealed record SoftCacheFacts(
    byte CacheBits,
    byte Associativity,
    SoftHashKind HashKind,
    SoftCacheConcurrency Concurrency,
    bool HasGlobalSeed,
    bool HasDebugMetrics
)
{
    public bool IsFitUshort => CacheBits == 16;

    public bool IsHasBucketed => Associativity > 1;

    public List<string> Keys { get; } = new();
}
