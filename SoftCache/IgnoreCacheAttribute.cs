using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoftCache;

/// <summary>
/// Marks a property or field to be ignored by the SoftCache source generator.
/// <para>
/// Any member decorated with this attribute will be excluded from:
/// <list type="bullet">
/// <item><description><see cref="ISoftCacheable{TParameters}.SoftEquals"/> comparison</description></item>
/// <item><description><see cref="ISoftCacheable{TParameters}.GetSoftHashCode"/> hash computation</description></item>
/// </list>
/// </para>
/// </summary>
/// <remarks>
/// Use this for members that do not affect the logical identity of the object
/// in the context of caching.  
/// Example: transient services, debug-only data, or fields that are always recomputed.
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class IgnoreCacheAttribute : Attribute
{
}