using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoftCache.Annotations;

/// <summary>
/// Declares cache generation options for a given <see cref="ISoftCacheable{TParameters}"/> type.
/// <para>
/// Attach this attribute to a partial type to control how the source generator will
/// implement and integrate the type with <c>SoftCache</c>.
/// </para>
/// </summary>
/// <remarks>
/// The attribute allows tuning of cache size, associativity, hash function,
/// concurrency policy, and debug metrics generation.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class SoftCacheAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftCacheAttribute"/> class.
    /// </summary>
    public SoftCacheAttribute() { }

    /// <summary>
    /// Size of the cache in bits.
    /// <para>
    /// The total number of slots will be <c>2^CacheBits</c>.
    /// Must be between 1 and 16 (inclusive).
    /// </para>
    /// </summary>
    public int CacheBits { get; set; } = 16;

    /// <summary>
    /// Number of entries per cache slot (set associativity).
    /// <para>
    /// A value of 1 means no buckets — direct-mapped cache.
    /// A value between 2 and 4 enables small buckets per slot, improving collision handling.
    /// </para>
    /// </summary>
    public int Associativity { get; set; } = 1;

    /// <summary>
    /// The hash algorithm used for <see cref="ISoftCacheable{TParameters}.GetSoftHashCode"/>.
    /// </summary>
    public SoftHashKind HashKind { get; set; } = SoftHashKind.XorFold16;

    /// <summary>
    /// Concurrency control mode used when writing into the cache.
    /// </summary>
    public SoftCacheConcurrency Concurrency { get; set; } = SoftCacheConcurrency.None;

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
    public bool GenerateGlobalSeed { get; set; } = false;

    /// <summary>
    /// Whether to include debug metrics collection for cache operations.
    /// <para>
    /// If <see langword="true"/>, the generator will insert counters for:
    /// cache hits, misses, evictions, and bucket collisions.
    /// Metrics are only active in <c>DEBUG</c> builds.
    /// </para>
    /// </summary>
    public bool EnableDebugMetrics { get; set; } = false;

    /// <summary>
    /// Optional domain key for multi-cache separation.
    /// <para>
    /// Types sharing the same domain value will use the same cache instance.
    /// Use to group related cached objects (e.g., by subsystem or DTO type family).
    /// </para>
    /// </summary>
    public string? Domain { get; set; }
}