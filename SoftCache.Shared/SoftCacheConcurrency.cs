namespace SoftCache.Annotations;

/// <summary>
/// Concurrency control policies for SoftCache writes.
/// </summary>
public enum SoftCacheConcurrency
{
    /// <summary>
    /// No synchronization.  
    /// Allows writes to race; newer value may overwrite older even if not identical.  
    /// Fastest, minimal overhead — used in Roslyn's green node cache.
    /// </summary>
    None,

    /// <summary>
    /// Optimistic write without compare-and-swap.  
    /// Assumes writes are rare and collisions unlikely; overwrites directly.
    /// </summary>
    Optimistic,

    /// <summary>
    /// Compare-and-swap only if the cache slot is empty.
    /// Prevents overwriting a valid entry unless it's a miss.
    /// </summary>
    CASOnEmpty,

    /// <summary>
    /// Compare-and-swap always before writing.  
    /// Safest, but slightly slower.
    /// </summary>
    CASAlways
}
