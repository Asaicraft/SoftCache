using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator.SoftCacheMakers;
public record struct CacheGenContext(
    SoftCacheOptions Options,
    INamedTypeSymbol TargetType,

    /// <summary>
    /// Actual number of slots in the cache, chosen by the generator.  
    /// Unlike <see cref="SoftCacheOptions.CacheBits"/>, this value is not constrained to powers of two.  
    /// Used directly in generated code for index calculation.
    /// </summary>
    int CacheSize,

    bool IsInternalElsePublic,

    // "global::Ns.Type"
    string FullyQualifiedTypeName,

    // "global::Ns.Type.Parameters"
    string ParametersTypeName,

    // "SoftCache"
    string CacheClassName,

    // "CacheMask"
    string CacheMaskName,

    // "Entry"
    string EntryStructName,

    // "s_cache"
    string CacheFieldName,

    string EntryLocal,

    string EntryValueLocal,

    string EntryHashLocal,

    string EntryStampLocal,

    string VictimIndexLocal,

    // "s_stamp"
    string StampFieldName,

    // "SoftCacheStats"
    string StatsName,

    // "idx" or "index" for variavle wich contains the index of the cache slot for exampe "var {IndexName} = hash & {CacheMaskName}"
    string IndexName,

    // for example "1", it's for "{CacheFieldName}[idx].s{IndexSuffix}"
    string? IndexSuffix
);

