using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator.SoftCacheMaker;
public sealed record CacheGenContext(
    SoftCacheOptions Options,
    INamedTypeSymbol TargetType,
    string FullyQualifiedTypeName,     // "global::Ns.Type"
    string ParametersTypeName,         // "global::Ns.Type.Parameters"
    string CacheClassName,             // "SoftCache"
    string EntryStructName,            // "Entry"
    string CacheFieldName,             // "s_cache"
    string StampFieldName              // "s_stamp"
);

