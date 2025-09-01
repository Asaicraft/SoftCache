using SoftCache.Annotations;
using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator.SoftCacheMaker.WritePolicyMakers;
/// <summary>
/// Write policy that uses compare-and-swap (CAS) only when claiming an empty slot during probing.
/// After a slot is claimed, the hash is published with a volatile write. Final writes for a chosen
/// victim fall back to the base non-atomic implementation.
/// </summary>
public sealed class CASOnEmptyWritePolicyMaker : WritePolicyMaker
{
    /// <summary>
    /// Uses CAS to claim an empty slot (value is null). On success, updates stamp (if LRU)
    /// and then publishes the hash with a volatile write. If a slot is considered empty by
    /// hash (h == 0) but value is not null, we still publish the hash to preserve original semantics.
    /// </summary>
    protected override IEnumerable<Microsoft.CodeAnalysis.CSharp.Syntax.StatementSyntax> AddEmptySlotProbe(CacheGenContext context)
    {
        var associativity = context.Options.Associativity;

        for (var i = 0; i < associativity; i++)
        {
            // Create refs for current way
            yield return Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseStatement($"ref var {context.EntryValueLocal} = ref entry.v{i};");
            yield return Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseStatement($"ref var {context.EntryHashLocal} = ref entry.h{i};");

            if (context.Options.Eviction == SoftCacheEvictionPolicy.LruApprox)
            {
                yield return Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseStatement($"ref var {context.EntryStampLocal} = ref entry.s{i};");
            }

            var setStamp = context.Options.Eviction == SoftCacheEvictionPolicy.LruApprox
                ? $"{context.EntryStampLocal} = global::System.Environment.TickCount;"
                : string.Empty;

            // Attempt to claim the slot by CAS on value: null -> value
            yield return Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseStatement(
                "if (" +
                    $"{context.EntryValueLocal} is null && " +
                    "global::System.Threading.Interlocked.CompareExchange(ref " +
                    $"{context.EntryValueLocal}, value, null) is null) " +
                $"{{ {setStamp} global::System.Threading.Volatile.Write(ref {context.EntryHashLocal}, hash); return; }}");

            // Fallback: if hash is zero, consider it empty and publish the hash
            yield return Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseStatement(
                $"if ({context.EntryHashLocal} == 0) {{ {setStamp} global::System.Threading.Volatile.Write(ref {context.EntryHashLocal}, hash); return; }}");
        }
    }
}