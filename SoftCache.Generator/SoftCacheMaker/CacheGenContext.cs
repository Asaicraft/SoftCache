using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator.SoftCacheMaker;
public sealed record CacheGenContext(
    SoftCacheOptions Options,
    INamedTypeSymbol TargetType,

    // "global::Ns.Type"
    string FullyQualifiedTypeName,

    // "global::Ns.Type.Parameters"
    string ParametersTypeName,

    // "SoftCache"
    string CacheClassName,

    // "Entry"
    string EntryStructName,

    // "s_cache"
    string CacheFieldName,

    // "s_stamp"
    string StampFieldName
);

