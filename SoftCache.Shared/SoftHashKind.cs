using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoftCache.Annotations;

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
    /// Custom hash logic — generator will call a partial method 
    /// <c>MakeSoftHash(in Parameters)</c> for manual implementation.
    /// </summary>
    Custom
}