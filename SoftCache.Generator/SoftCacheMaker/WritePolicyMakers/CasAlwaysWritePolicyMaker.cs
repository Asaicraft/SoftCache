using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using SoftCache.Annotations;

namespace SoftCache.Generator.SoftCacheMaker.WritePolicyMakers;

/// <summary>
/// Compare-and-swap (CAS) policy that always uses atomic operations when writing values.
/// Strategy:
/// - For empty-slot probe: try to claim a slot by CAS on the value reference (null -> value).
///   On success, publish the stamp (if LRU) and then publish the hash with a volatile write.
/// - For victim write: atomically swap the value (Interlocked.Exchange), then publish stamp/hash.
/// Notes:
/// - We CAS on value because it is a reference type (checked against null), which has convenient generic CAS.
/// - Hash is published with a volatile write to ensure readers see a fully-initialized value first.
/// </summary>
public sealed class CasAlwaysWritePolicyMaker : WritePolicyMaker
{
    public static readonly CasAlwaysWritePolicyMaker Instance = new();

    protected override IEnumerable<StatementSyntax> AddEmptySlotProbe(CacheGenContext context)
    {
        var associativity = context.Options.Associativity;

        for (var i = 0; i < associativity; i++)
        {
            // Refs into entry for the current way
            yield return ParseStatement($"ref var {context.EntryValueLocal} = ref entry.v{i};");
            yield return ParseStatement($"ref var {context.EntryHashLocal} = ref entry.h{i};");
            if (context.Options.Eviction == SoftCacheEvictionPolicy.LruApprox)
            {
                yield return ParseStatement($"ref var {context.EntryStampLocal} = ref entry.s{i};");
            }

            var setStamp = context.Options.Eviction == SoftCacheEvictionPolicy.LruApprox
                ? $"{context.EntryStampLocal} = global::System.Environment.TickCount;"
                : string.Empty;

            // Try to claim the slot by CAS on value: null -> value
            // If we won the CAS, publish stamp (if any) and then publish hash (volatile write).
            yield return ParseStatement(
                "if (" +
                    $"{context.EntryValueLocal} is null && " +
                    "global::System.Threading.Interlocked.CompareExchange(ref " +
                    $"{context.EntryValueLocal}, value, null) is null) " +
                $"{{ {setStamp} global::System.Threading.Volatile.Write(ref {context.EntryHashLocal}, hash); return; }}");

            // Fallback: if slot is effectively empty by hash (but value is not null), publish hash and return.
            // This keeps the original semantics for the (h == 0) empty condition.
            yield return ParseStatement(
                $"if ({context.EntryHashLocal} == 0) {{ {setStamp} global::System.Threading.Volatile.Write(ref {context.EntryHashLocal}, hash); return; }}");
        }
    }

    protected override IEnumerable<StatementSyntax> AddFinalWrite(CacheGenContext context)
    {
        // Always use CAS/atomic style for final writes.
        var associativity = context.Options.Associativity;

        if (associativity == 1)
        {
            foreach (var s in AddFinalWriteDirectMappedCas(context))
            {
                yield return s;
            }

            yield break;
        }

        foreach (var s in AddFinalWriteSetAssociativeCas(context))
        {
            yield return s;
        }
    }

    /// <summary>
    /// Direct-mapped final write using atomic exchange for the value,
    /// then publishing stamp and hash.
    /// </summary>
    private IEnumerable<StatementSyntax> AddFinalWriteDirectMappedCas(CacheGenContext context)
    {
        // Atomically replace value; we do not need the returned previous value here.
        yield return ParseStatement("global::System.Threading.Interlocked.Exchange(ref entry.v0, value);");

        // Stamp (if LRU policy)
        foreach (var stamp in BuildStampAssignment("entry.s0", context))
        {
            yield return stamp;
        }

        // Publish hash with a volatile write
        yield return ParseStatement("global::System.Threading.Volatile.Write(ref entry.h0, hash);");
        yield return ParseStatement("return;");
    }

    /// <summary>
    /// Set-associative final write using atomic exchange for the selected victim way,
    /// then publishing stamp and hash.
    /// </summary>
    private IEnumerable<StatementSyntax> AddFinalWriteSetAssociativeCas(CacheGenContext context)
    {
        var sections = new List<SwitchSectionSyntax>();
        for (var i = 0; i < context.Options.Associativity; i++)
        {
            sections.Add(BuildCasSection(context, i));
        }

        sections.Add(BuildCasDefaultSection(context));

        yield return SwitchStatement(IdentifierName(context.VictimIndexLocal))
            .WithSections(List(sections));
    }

    private SwitchSectionSyntax BuildCasSection(CacheGenContext context, int way)
    {
        var statements = new List<StatementSyntax>
            {
                // Atomic swap of value
                ParseStatement($"global::System.Threading.Interlocked.Exchange(ref entry.v{way}, value);")
            };

        // Stamp (if any)
        foreach (var stamp in BuildStampAssignment($"entry.s{way}", context))
        {
            statements.Add(stamp);
        }

        // Publish hash
        statements.Add(ParseStatement($"global::System.Threading.Volatile.Write(ref entry.h{way}, hash);"));
        statements.Add(ParseStatement("return;"));

        return SwitchSection()
            .WithLabels(SingletonList<SwitchLabelSyntax>(
                CaseSwitchLabel(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(way)))))
            .WithStatements(List(statements));
    }

    private SwitchSectionSyntax BuildCasDefaultSection(CacheGenContext context)
    {
        var statements = new List<StatementSyntax>
            {
                ParseStatement("global::System.Threading.Interlocked.Exchange(ref entry.v0, value);")
            };

        foreach (var stamp in BuildStampAssignment("entry.s0", context))
        {
            statements.Add(stamp);
        }

        statements.Add(ParseStatement("global::System.Threading.Volatile.Write(ref entry.h0, hash);"));
        statements.Add(ParseStatement("return;"));

        return SwitchSection()
            .WithLabels(SingletonList<SwitchLabelSyntax>(DefaultSwitchLabel()))
            .WithStatements(List(statements));
    }
}