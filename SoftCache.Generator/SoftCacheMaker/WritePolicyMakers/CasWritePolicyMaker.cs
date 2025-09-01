using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using SoftCache.Annotations;

namespace SoftCache.Generator.SoftCacheMakers.WritePolicyMakers;

/// <summary>
/// CAS writer with per-entry version flag (even = stable, odd = write-in-progress).
/// Acquires write-token by CAS on 'version', mutates a local copy, assigns the whole entry back, then publishes even version.
/// </summary>
public sealed class CasWritePolicyMaker : WritePolicyMaker
{
    public static readonly CasWritePolicyMaker Instance = new();

    /// <summary>
    /// Empty-slot fast path with version CAS:
    /// - Try to acquire (even -> odd) via CompareExchange on entry.version.
    /// - If acquired, build newEntry from entry, set value/stamp/hash, assign whole struct, publish version even.
    /// </summary>
    protected override IEnumerable<StatementSyntax> AddEmptySlotProbe(CacheGenContext cacheGenerationContext)
    {
        var associativity = cacheGenerationContext.Options.Associativity;
        var useTimestamp = cacheGenerationContext.Options.Eviction == SoftCacheEvictionPolicy.LruApprox;

        for (int index = 0; index < associativity; index++)
        {
            var statement =
                $"if (entry.h{index} == 0 || entry.v{index} is null) " +
                "{{ " +
                    "var version = global::System.Threading.Volatile.Read(ref entry.version); " +
                    "if ((version & 1u) == 0u && " +
                        "global::System.Threading.Interlocked.CompareExchange(ref entry.version, version + 1u, version) == version) " +
                    "{{ " +
                        "var newEntry = entry; " +
                        $"newEntry.v{index} = value; " +
                        (useTimestamp ? $"newEntry.s{index} = global::System.Environment.TickCount; " : string.Empty) +
                        $"newEntry.h{index} = hash; " +
                        "entry = newEntry; " +
                        "global::System.Threading.Volatile.Write(ref entry.version, version + 2u); " +
                        "return; " +
                    "}} " +
                "}}";

            yield return ParseStatement(statement);
        }
    }

    /// <summary>
    /// Victim selection is inherited from base (Overwrite or LruApprox).
    /// </summary>
    protected override IEnumerable<StatementSyntax> AddVictimSelection(CacheGenContext cacheGenerationContext)
    {
        foreach (var statement in base.AddVictimSelection(cacheGenerationContext))
        {
            yield return statement;
        }
    }

    /// <summary>
    /// Direct-mapped final write with version CAS:
    /// Acquire -> build newEntry -> assign whole entry -> publish even version -> return.
    /// </summary>
    protected override IEnumerable<StatementSyntax> AddFinalWriteDirectMapped(CacheGenContext cacheGenerationContext)
    {
        var useTimestamp = cacheGenerationContext.Options.Eviction == SoftCacheEvictionPolicy.LruApprox;

        yield return ParseStatement("var version = global::System.Threading.Volatile.Read(ref entry.version);");
        yield return ParseStatement(
            "if ((version & 1u) == 0u && " +
            "global::System.Threading.Interlocked.CompareExchange(ref entry.version, version + 1u, version) == version) " +
            "{ " +
                "var newEntry = entry; " +
                "newEntry.v0 = value; " +
                (useTimestamp ? "newEntry.s0 = global::System.Environment.TickCount; " : string.Empty) +
                "newEntry.h0 = hash; " +
                "entry = newEntry; " +
                "global::System.Threading.Volatile.Write(ref entry.version, version + 2u); " +
                "return; " +
            "}"
        );
    }

    /// <summary>
    /// Set-associative final write:
    /// switch(victimIndex) { acquire version; newEntry = entry; mutate chosen way; entry = newEntry; publish even; return; }
    /// </summary>
    protected override IEnumerable<StatementSyntax> AddFinalWriteSetAssociative(CacheGenContext cacheGenerationContext)
    {
        var sections = new List<SwitchSectionSyntax>();
        var useTimestamp = cacheGenerationContext.Options.Eviction == SoftCacheEvictionPolicy.LruApprox;

        for (int wayIndex = 0; wayIndex < cacheGenerationContext.Options.Associativity; wayIndex++)
        {
            var statements = new List<StatementSyntax>
            {
                ParseStatement("var version = global::System.Threading.Volatile.Read(ref entry.version);"),
                ParseStatement(
                    "if ((version & 1u) == 0u && " +
                    "global::System.Threading.Interlocked.CompareExchange(ref entry.version, version + 1u, version) == version) " +
                    "{ " +
                        "var newEntry = entry; " +
                        $"newEntry.v{wayIndex} = value; " +
                        (useTimestamp ? $"newEntry.s{wayIndex} = global::System.Environment.TickCount; " : string.Empty) +
                        $"newEntry.h{wayIndex} = hash; " +
                        "entry = newEntry; " +
                        "global::System.Threading.Volatile.Write(ref entry.version, version + 2u); " +
                        "return; " +
                    "}"
                )
            };

            sections.Add(
                SwitchSection()
                    .WithLabels(SingletonList<SwitchLabelSyntax>(
                        CaseSwitchLabel(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(wayIndex)))))
                    .WithStatements(List(statements)));
        }

        var defaultStatements = new List<StatementSyntax>
        {
            ParseStatement("var version = global::System.Threading.Volatile.Read(ref entry.version);"),
            ParseStatement(
                "if ((version & 1u) == 0u && " +
                "global::System.Threading.Interlocked.CompareExchange(ref entry.version, version + 1u, version) == version) " +
                "{ " +
                    "var newEntry = entry; " +
                    "newEntry.v0 = value; " +
                    (useTimestamp ? "newEntry.s0 = global::System.Environment.TickCount; " : string.Empty) +
                    "newEntry.h0 = hash; " +
                    "entry = newEntry; " +
                    "global::System.Threading.Volatile.Write(ref entry.version, version + 2u); " +
                    "return; " +
                "}"
            )
        };

        sections.Add(
            SwitchSection()
                .WithLabels(SingletonList<SwitchLabelSyntax>(DefaultSwitchLabel()))
                .WithStatements(List(defaultStatements)));

        yield return SwitchStatement(IdentifierName(cacheGenerationContext.VictimIndexLocal)).WithSections(List(sections));
    }
}
