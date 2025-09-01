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
    /// Compare-and-swap–style publication.  
    /// Writes rebuild the entry and publish it using <see cref="System.Threading.Volatile.Write{T}(ref T, T)"/>,  
    /// ensuring that updates become visible in the correct order (value/stamp → hash).  
    /// Prevents consumers from observing partially updated entries,  
    /// but does not guarantee atomic replacement across multiple threads like a full CAS loop.  
    /// Suitable for scenarios where ordering and visibility matter but lock-free retry loops are unnecessary.
    /// </summary>
    CAS,

    /// <summary>
    /// Full mutual exclusion via locking.  
    /// Writers acquire a monitor or other synchronization primitive before modifying the cache entry,  
    /// ensuring exclusive access and strong consistency across threads.  
    /// This policy eliminates write races entirely but has the highest runtime overhead.
    /// </summary>
    Lock,
}
