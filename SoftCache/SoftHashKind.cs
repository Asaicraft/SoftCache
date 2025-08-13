using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoftCache;

/// <summary>
/// Built-in lightweight hash algorithms for SoftCache.
/// </summary>
public enum SoftHashKind
{
    /// <summary>
    /// Fast XOR folding of integer halves into 16 bits.  
    /// Default — minimal CPU cost, decent distribution for simple data.
    /// </summary>
    XorFold16,

    /// <summary>
    /// Reduced 16-bit Murmur3 mix.  
    /// Slower, but better avalanche and resistance to patterned inputs.
    /// </summary>
    Murmur3_16,

    /// <summary>
    /// Reduced 16-bit FNV-1a hash.  
    /// Compact and simple, widely used for small key spaces.
    /// </summary>
    Fnv1a_16,

    /// <summary>
    /// Custom hash logic — generator will call a partial method 
    /// <c>CombineSoftHash(...)</c> for manual implementation.
    /// </summary>
    Custom
}