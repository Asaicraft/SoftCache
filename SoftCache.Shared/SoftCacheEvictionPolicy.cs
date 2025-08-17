namespace SoftCache.Annotations;

public enum SoftCacheEvictionPolicy
{
    /// <summary>
    /// Replace the first slot (fast, simple, more churn).
    /// </summary>
    Overwrite = 0,

    /// <summary>
    /// Replace the least-recently-touched slot using a monotonic stamp (approximate LRU).
    /// </summary>
    LruApprox = 1
}