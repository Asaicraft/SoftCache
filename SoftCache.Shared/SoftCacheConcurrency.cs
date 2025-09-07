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
    /// Full mutual exclusion via locking.  
    /// Writers acquire a monitor or other synchronization primitive before modifying the cache entry,  
    /// ensuring exclusive access and strong consistency across threads.  
    /// This policy eliminates write races entirely but has the highest runtime overhead.
    /// </summary>
    Lock,
}
