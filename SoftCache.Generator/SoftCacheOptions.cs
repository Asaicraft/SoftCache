using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator;

public sealed record SoftCacheOptions
{
    /// <summary>
    /// Size of the cache in bits.
    /// <para>
    /// The total number of slots will be <c>2^CacheBits</c>.
    /// Must be between 1 and 16 (inclusive).
    /// </para>
    /// </summary>
    public int CacheBits { get; init; } = 16;

    /// <summary>
    /// Number of entries per cache slot (init associativity).
    /// <para>
    /// A value of 1 means no buckets — direct-mapped cache.
    /// A value between 2 and 4 enables small buckets per slot, improving collision handling.
    /// </para>
    /// </summary>
    public int Associativity { get; init; } = 1;

    /// <summary>
    /// The hash algorithm used for <see cref="ISoftCacheable{TParameters}.GetSoftHashCode"/>.
    /// </summary>
    public SoftHashKind HashKind { get; init; } = SoftHashKind.XorFold16;

    /// <summary>
    /// Concurrency control mode used when writing into the cache.
    /// </summary>
    public SoftCacheConcurrency Concurrency { get; init; } = SoftCacheConcurrency.None;

    /// <summary>
    /// Whether to generate a random global seed for the hash function at application startup.
    /// <para>
    /// If <see langword="true"/>, the generator will insert a 
    /// <c>RandomNumberGenerator.GetBytes(2)</c> call to produce a per-process 
    /// 16-bit salt that is mixed into all hash codes.
    /// </para>
    /// <para>
    /// This adds a small amount of hash randomization for low-probability 
    /// collision avoidance and minor security benefits.
    /// </para>
    /// </summary>
    public bool GenerateGlobalSeed { get; init; } = false;

    /// <summary>
    /// Whether to include debug metrics collection for cache operations.
    /// <para>
    /// If <see langword="true"/>, the generator will insert counters for:
    /// cache hits, misses, evictions, and bucket collisions.
    /// Metrics are only active in <c>DEBUG</c> builds.
    /// </para>
    /// </summary>
    public bool EnableDebugMetrics { get; init; } = false;

    /// <summary>
    /// Optional domain key for multi-cache separation.
    /// <para>
    /// Types sharing the same domain value will use the same cache instance.
    /// Use to group related cached objects (e.g., by subsystem or DTO type family).
    /// </para>
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// The target type for which the cache is being generated.
    /// </summary>
    public ITypeSymbol TargetType { get; init; }
}
