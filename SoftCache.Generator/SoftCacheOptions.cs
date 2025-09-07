using Microsoft.CodeAnalysis;
using SoftCache.Annotations;
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
    /// A value of 1 means no buckets - direct-mapped cache.
    /// A value between 2 and 4 enables small buckets per slot, improving collision handling.
    /// </para>
    /// </summary>
    public int Associativity { get; init; } = 1;

    /// <summary>
    /// The hash algorithm used for <see cref="ISoftCacheable{TParameters}.GetSoftHashCode"/>.
    /// </summary>
    public SoftHashKind HashKind { get; init; } = SoftHashKind.XorFold;

    /// <summary>
    /// Concurrency control mode used when writing into the cache.
    /// </summary>
    public SoftCacheConcurrency Concurrency { get; init; } = SoftCacheConcurrency.None;

    /// <summary>
    /// Eviction policy when a bucket is full.
    /// </summary>
    public SoftCacheEvictionPolicy Eviction { get; init; } = SoftCacheEvictionPolicy.LruApprox;

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
    /// The target type for which the cache is being generated.
    /// </summary>
    public required ITypeSymbol TargetType { get; init; }

    /// <summary>
    /// Whether to replace <c>2^CacheBits</c> with the nearest strictly greater prime number 
    /// for the actual cache size (<see cref="CacheGenContext.CacheSize"/>).
    /// <para>
    /// This improves key distribution when <c>CacheBits &lt; 16</c>, because indices are 
    /// computed modulo a prime rather than a power of two, reducing clustering effects.
    /// </para>
    /// <para>
    /// When <c>CacheBits == 16</c>, the option is ignored: the hash is already 16-bit wide,
    /// and can be used directly as an index without masking or modular reduction.
    /// </para>
    /// </summary>
    public bool UseNearestPrime { get; init; } = false;

    /// <summary>
    /// Whether the <see cref="System.Threading.Lock"/> type is available in the target runtime.
    /// <para>
    /// When <see langword="true"/>, the generator will prefer emitting code that uses 
    /// <c>System.Threading.Lock</c> (introduced in .NET 9) for concurrency control. 
    /// </para>
    /// </summary>
    public bool HasSystemThreadingLock { get; init; } = false;
}
