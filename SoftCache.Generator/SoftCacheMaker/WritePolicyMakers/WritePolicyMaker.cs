using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using SoftCache.Annotations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SoftCache.Generator.SoftCacheMaker.WritePolicyMakers;

/// <summary>
/// Base “template method” for generating <c>Add(value, hash)</c>.
/// <list type="number">
/// <item><description>Computes the bucket <c>index</c> based on <paramref name="hash"/> and cache size.</description></item>
/// <item><description>Loads the target <c>entry</c> (bucket) by reference.</description></item>
/// <item><description>Probes for an empty slot within the bucket (if probing is enabled).</description></item>
/// <item><description>Selects a victim slot when the bucket is full (policy-driven).</description></item>
/// <item><description>Performs the write (optionally wrapping it for CAS-like policies).</description></item>
/// </list>
/// Derivations override one or more hooks without rewriting the whole method.
/// </summary>
/// <remarks>
/// This type builds Roslyn syntax trees to generate the actual writer method at source-gen time.
/// It is not the writer itself; instead, it composes method bodies from overridable stages.
/// </remarks>
/// <seealso cref="IWritePolicyMaker"/>
public abstract class WritePolicyMaker : IWritePolicyMaker
{
    /// <summary>
    /// Creates a concrete <see cref="IWritePolicyMaker"/> for the provided <see cref="CacheGenContext"/>.
    /// </summary>
    /// <param name="context">Generation context containing cache layout, options, and naming conventions.</param>
    /// <returns>
    /// A policy maker instance suitable for <see cref="SoftCacheConcurrency.None"/>; other modes are currently unsupported.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when <see cref="CacheGenOptions.Concurrency"/> is not supported by this implementation.
    /// </exception>
    public static IWritePolicyMaker CreateWritePolicyMaker(CacheGenContext context)
    {
        return context.Options.Concurrency switch
        {
            SoftCacheConcurrency.None => SimpleWritePolicyMaker.Instance,
            _ => throw new NotSupportedException()
        };
    }

    /// <summary>
    /// Composes the <c>Add(value, hash)</c> method declaration and body using the template steps.
    /// </summary>
    /// <param name="context">Generation context with cache configuration and symbols.</param>
    /// <returns>
    /// A <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax"/> representing the generated writer.
    /// </returns>
    /// <remarks>
    /// The body is assembled from the following overridable hooks:
    /// <see cref="AddDebugInfoIfNeeded"/>, <see cref="AddIndexSelector"/>, <see cref="AddEntryReference"/>,
    /// <see cref="AddEmptySlotProbe"/>, <see cref="AddVictimSelection"/>, and <see cref="AddFinalWrite"/>.
    /// </remarks>
    public virtual MethodDeclarationSyntax CreateWriter(CacheGenContext context)
    {
        var method = MethodDeclaration(
                PredefinedType(Token(SyntaxKind.VoidKeyword)),
                Identifier("Add"))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SeparatedList(
            [
                Parameter(Identifier("value"))
                    .WithType(ParseTypeName(context.FullyQualifiedTypeName)),
                Parameter(Identifier("hash"))
                    .WithType(PredefinedType(Token(SyntaxKind.UShortKeyword)))
            ])));

        var body = new List<StatementSyntax>();

        body.AddRange(AddDebugInfoIfNeeded(context));
        body.AddRange(AddIndexSelector(context));
        body.AddRange(AddEntryReference(context));
        body.AddRange(AddEmptySlotProbe(context));
        body.AddRange(AddVictimSelection(context));
        body.AddRange(AddFinalWrite(context));

        return method.WithBody(Block(body));
    }

    /// <summary>
    /// Optionally emits debug/metrics statements at the start of the writer.
    /// </summary>
    /// <param name="context">Generation context providing <see cref="CacheGenOptions.EnableDebugMetrics"/> and symbol names.</param>
    /// <returns>
    /// An enumeration of <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.StatementSyntax"/> to prepend to the body.
    /// Yields nothing when debug metrics are disabled.
    /// </returns>
    protected virtual IEnumerable<StatementSyntax> AddDebugInfoIfNeeded(CacheGenContext context)
    {
        if (context.Options.EnableDebugMetrics)
        {
            yield return ParseStatement($"{context.FullyQualifiedTypeName}.{context.StatsName}.ItemAdded();");
        }
    }

    /// <summary>
    /// Emits code that computes the target bucket index from <c>hash</c>.
    /// </summary>
    /// <param name="context">Generation context providing cache size and naming.</param>
    /// <returns>
    /// Statements that assign the computed index to a variable named <see cref="CacheGenContext.IndexName"/>.
    /// Uses a specialized fast modulo when possible.
    /// </returns>
    /// <remarks>
    /// For <c>CacheBits == 16</c> the index is a simple unchecked cast; otherwise a 32×32-&gt;64 fast-mod pattern is inlined.
    /// </remarks>
    protected virtual IEnumerable<StatementSyntax> AddIndexSelector(CacheGenContext context)
    {
        var cacheSize = 1 << context.Options.CacheBits;
        var fastModMultiplier = (ulong.MaxValue / (uint)cacheSize) + 1UL;

        string expression;
        if (context.Options.CacheBits == 16)
        {
            // Special case: just cast ushort to int, no need for masking
            expression = "unchecked((int)hash)";
        }
        else
        {
            // Embed FastMod directly into generated source
            expression = $"(int)((((({fastModMultiplier}UL * (ulong)hash) >> 32) + 1) * {cacheSize} >> 32))";
        }

        yield return ParseStatement($"var {context.IndexName} = {expression};");
    }

    /// <summary>
    /// Emits code that loads the target bucket entry by reference.
    /// </summary>
    /// <param name="context">Generation context providing cache field name and the local entry variable name.</param>
    /// <returns>
    /// A single statement assigning <c>ref</c> to <see cref="CacheGenContext.EntryLocal"/> from <see cref="CacheGenContext.CacheFieldName"/> at <see cref="CacheGenContext.IndexName"/>.
    /// </returns>
    protected virtual IEnumerable<StatementSyntax> AddEntryReference(CacheGenContext context)
    {
        yield return ParseStatement($"ref var {context.EntryLocal} = ref {context.CacheFieldName}[{context.IndexName}];");
    }

    /// <summary>
    /// Emits a linear probe over all ways in the bucket to find an empty slot.
    /// </summary>
    /// <param name="context">Generation context providing associativity, eviction policy, and local names.</param>
    /// <returns>
    /// For each way, declares <c>ref</c> locals for value/hash (and stamp when LRU-approx is used) and performs an early-return write if the slot is empty.
    /// </returns>
    /// <remarks>
    /// An empty slot is identified when the stored hash is zero or the stored value is <c>null</c>.
    /// For <see cref="SoftCacheEvictionPolicy.LruApprox"/>, the access stamp is updated using <see cref="Environment.TickCount"/>.
    /// </remarks>
    protected virtual IEnumerable<StatementSyntax> AddEmptySlotProbe(CacheGenContext context)
    {
        var associativity = context.Options.Associativity;
        for (int i = 0; i < associativity; i++)
        {
            yield return ParseStatement($"ref var {context.EntryValueLocal} = ref entry.v{i};");
            yield return ParseStatement($"ref var {context.EntryHashLocal} = ref entry.h{i};");

            if (context.Options.Eviction == SoftCacheEvictionPolicy.LruApprox)
            {
                yield return ParseStatement($"ref var {context.EntryStampLocal} = ref entry.s{i};");
            }

            var setStamp = context.Options.Eviction == SoftCacheEvictionPolicy.LruApprox
                ? $"{context.EntryStampLocal} = global::System.Environment.TickCount;"
                : string.Empty;

            yield return ParseStatement(
                $"if ({context.EntryHashLocal} == 0 || {context.EntryValueLocal} is null) {{ {context.EntryValueLocal} = value; {setStamp} {context.EntryHashLocal} = hash; return; }}");
        }
    }

    /// <summary>
    /// Emits code to select a victim way when the bucket is full.
    /// </summary>
    /// <param name="context">Generation context providing associativity and eviction policy.</param>
    /// <returns>
    /// Statements that set <see cref="CacheGenContext.VictimIndexLocal"/> to the selected way.
    /// For <see cref="SoftCacheEvictionPolicy.Overwrite"/>, way 0 is chosen.
    /// For <see cref="SoftCacheEvictionPolicy.LruApprox"/>, the way with the smallest stamp is chosen.
    /// </returns>
    protected virtual IEnumerable<StatementSyntax> AddVictimSelection(CacheGenContext context)
    {
        var associativity = context.Options.Associativity;

        if (associativity == 1)
        {
            yield break;
        }

        if (context.Options.Eviction == SoftCacheEvictionPolicy.Overwrite)
        {
            yield return ParseStatement($"int {context.VictimIndexLocal} = 0;");
            yield break;
        }

        yield return ParseStatement($"int {context.VictimIndexLocal} = 0;");
        yield return ParseStatement("int __min = entry.s0;");

        for (var i = 1; i < associativity; i++)
        {
            yield return ParseStatement($"if (entry.s{i} < __min) {{ __min = entry.s{i}; {context.VictimIndexLocal} = {i}; }}");
        }
    }

    /// <summary>
    /// Emits the final write to the selected way and returns from the generated method.
    /// </summary>
    /// <param name="context">Generation context providing associativity and names.</param>
    /// <returns>
    /// For direct-mapped caches, writes to way 0; for set-associative caches, switches on <see cref="CacheGenContext.VictimIndexLocal"/>.
    /// </returns>
    protected virtual IEnumerable<StatementSyntax> AddFinalWrite(CacheGenContext context)
    {
        var associativity = context.Options.Associativity;

        if (associativity == 1)
        {
            foreach (var statement in AddFinalWriteDirectMapped(context))
            {
                yield return statement;
            }
            yield break;
        }

        foreach (var statement in AddFinalWriteSetAssociative(context))
        {
            yield return statement;
        }
    }

    /// <summary>
    /// Emits the final write for direct-mapped caches (associativity = 1).
    /// </summary>
    /// <param name="context">Generation context.</param>
    /// <returns>
    /// Statements that assign value, optionally update the stamp, assign hash, and return.
    /// </returns>
    protected virtual IEnumerable<StatementSyntax> AddFinalWriteDirectMapped(CacheGenContext context)
    {
        yield return ParseStatement("entry.v0 = value;");

        foreach (var stamp in BuildStampAssignment("entry.s0", context))
        {
            yield return stamp;
        }

        yield return ParseStatement("entry.h0 = hash;");
        yield return ParseStatement("return;");
    }

    /// <summary>
    /// Emits the final write for set-associative caches as a <c>switch</c> over the victim way.
    /// </summary>
    /// <param name="context">Generation context.</param>
    /// <returns>
    /// A <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.SwitchStatementSyntax"/> that writes to the selected way and returns.
    /// </returns>
    protected virtual IEnumerable<StatementSyntax> AddFinalWriteSetAssociative(CacheGenContext context)
    {
        var sections = new List<SwitchSectionSyntax>();

        for (int i = 0; i < context.Options.Associativity; i++)
        {
            sections.Add(BuildSwitchSectionForWay(context, i));
        }

        sections.Add(BuildDefaultSwitchSection(context));

        yield return SwitchStatement(IdentifierName(context.VictimIndexLocal)).WithSections(List(sections));
    }

    /// <summary>
    /// Builds a <c>switch</c> section that writes into the specified way.
    /// </summary>
    /// <param name="context">Generation context.</param>
    /// <param name="way">The way (index) to write.</param>
    /// <returns>
    /// A <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.SwitchSectionSyntax"/> that assigns value, updates stamp (if applicable), assigns hash, and returns.
    /// </returns>
    protected virtual SwitchSectionSyntax BuildSwitchSectionForWay(CacheGenContext context, int way)
    {
        var statements = new List<StatementSyntax>
        {
            ParseStatement($"entry.v{way} = value;")
        };

        foreach (var stamp in BuildStampAssignment($"entry.s{way}", context))
        {
            statements.Add(stamp);
        }

        statements.Add(ParseStatement($"entry.h{way} = hash;"));
        statements.Add(ParseStatement("return;"));

        return SwitchSection()
            .WithLabels(SingletonList<SwitchLabelSyntax>(CaseSwitchLabel(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(way)))))
            .WithStatements(List(statements));
    }

    /// <summary>
    /// Builds the default <c>switch</c> section used as a safety net when the victim index is out of range.
    /// </summary>
    /// <param name="context">Generation context.</param>
    /// <returns>
    /// A <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.SwitchSectionSyntax"/> that writes to way 0 and returns.
    /// </returns>
    protected virtual SwitchSectionSyntax BuildDefaultSwitchSection(CacheGenContext context)
    {
        var statements = new List<StatementSyntax>
        {
            ParseStatement("entry.v0 = value;")
        };

        foreach (var stamp in BuildStampAssignment("entry.s0", context))
        {
            statements.Add(stamp);
        }

        statements.Add(ParseStatement("entry.h0 = hash;"));
        statements.Add(ParseStatement("return;"));

        return SwitchSection()
            .WithLabels(SingletonList<SwitchLabelSyntax>(DefaultSwitchLabel()))
            .WithStatements(List(statements));
    }

    /// <summary>
    /// Conditionally emits an assignment to the access timestamp for LRU-approx eviction.
    /// </summary>
    /// <param name="left">The left-hand side expression that represents the stamp field (e.g., <c>entry.s0</c>).</param>
    /// <param name="context">Generation context providing the eviction policy.</param>
    /// <returns>
    /// A single assignment to <paramref name="left"/> when <see cref="SoftCacheEvictionPolicy.LruApprox"/> is enabled; otherwise yields nothing.
    /// </returns>
    /// <remarks>
    /// Uses <see cref="Environment.TickCount"/> as a coarse-grained, monotonic-ish timestamp suitable for approximate LRU.
    /// </remarks>
    protected virtual IEnumerable<StatementSyntax> BuildStampAssignment(string left, CacheGenContext context)
    {
        if (context.Options.Eviction != SoftCacheEvictionPolicy.LruApprox)
        {
            yield break;
        }

        yield return ParseStatement($"{left} = global::System.Environment.TickCount;");
    }
}