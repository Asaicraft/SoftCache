using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using SoftCache.Annotations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using SoftCache.Generator.SoftCacheMakers.IndexSelectors;

namespace SoftCache.Generator.SoftCacheMakers.WritePolicyMakers;

/// <summary>
/// Base “template method” for generating <c>Add(value, hash)</c>.
/// <list type="number">
/// <item><description>Computes the bucket <c>index</c> based on <paramref name="hash"/> and cache size.</description></item>
/// <item><description>Loads the target <c>entry</c> (bucket) by reference.</description></item>
/// <item><description>Probes for an empty slot within the bucket and, if found, updates a local copy of the entry and assigns it back.</description></item>
/// <item><description>Selects a victim slot when the bucket is full (policy-driven).</description></item>
/// <item><description>Performs the final write by rebuilding the entry locally and assigning it back to the bucket entry.</description></item>
/// </list>
/// Derivations override one or more hooks without rewriting the whole method.
/// </summary>
/// <remarks>
/// <para>
/// This type composes Roslyn syntax trees that generate the actual writer method at source-generation time.
/// It is not the writer itself; instead, it emits method bodies assembled from overridable stages (hooks).
/// </para>
/// <para>
/// <b>Write semantics:</b> successful writes replace the entire bucket entry via simple assignment
/// (e.g., <c>entry = newEntry;</c>). This version targets <see cref="SoftCacheConcurrency.None"/> and does not
/// introduce additional memory-ordering guarantees such as <c>volatile</c> or interlocked operations.
/// </para>
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
            SoftCacheConcurrency.CAS => CasWritePolicyMaker.Instance,
            SoftCacheConcurrency.Lock => LockWritePolicyMaker.Instance,
            _ => throw new NotSupportedException()
        };
    }

    /// <summary>
    /// Composes the <c>Add(value, hash)</c> method declaration and body using the template steps.
    /// </summary>
    /// <param name="context">Generation context with cache configuration and symbol/name providers.</param>
    /// <returns>
    /// A <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax"/> representing the generated writer.
    /// </returns>
    /// <remarks>
    /// The body is assembled from the following hooks (in order):
    /// <see cref="AddDebugInfoIfNeeded"/>, <see cref="AddIndexSelector"/>, <see cref="AddEntryReference"/>,
    /// <see cref="AddEmptySlotProbe"/>, <see cref="AddVictimSelection"/>, and <see cref="AddFinalWrite"/>.
    /// </remarks>
    public virtual MethodDeclarationSyntax CreateWriter(CacheGenContext context)
    {
        var method = CreateAddHeader(context);
        var body = Block(BuildPipelineStatements(context));
        return method.WithBody(body);
    }

    /// <summary>
    /// Unified header for: public static void Add(T value, uint hash)
    /// </summary>
    protected virtual MethodDeclarationSyntax CreateAddHeader(CacheGenContext context)
    {
        return MethodDeclaration(
                PredefinedType(Token(SyntaxKind.VoidKeyword)),
                Identifier("Add"))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(
                ParameterList(SeparatedList(
                [
                    Parameter(Identifier("value"))
                        .WithType(ParseTypeName(context.FullyQualifiedTypeName)),
                    Parameter(Identifier("hash"))
                        .WithType(PredefinedType(Token(SyntaxKind.UIntKeyword)))
                ])));
    }

    /// <summary>
    /// Builds the pipeline of statements that make up the writer method.
    /// This method orchestrates the sequence of steps required to generate
    /// the `Add(value, hash)` method body, delegating to specific hooks for
    /// each stage of the pipeline.
    /// </summary>
    /// <param name="context">The generation context containing cache configuration and naming conventions.</param>
    /// <returns>
    /// An enumerable of <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.StatementSyntax"/> 
    /// representing the pipeline of statements for the writer method.
    /// </returns>
    protected virtual IEnumerable<StatementSyntax> BuildPipelineStatements(CacheGenContext context)
    {
        var pipelineStatements = new List<StatementSyntax>();
        pipelineStatements.AddRange(AddDebugInfoIfNeeded(context));
        pipelineStatements.AddRange(AddIndexSelector(context));
        pipelineStatements.AddRange(AddEntryReference(context));
        pipelineStatements.AddRange(AddEmptySlotProbe(context));
        pipelineStatements.AddRange(AddVictimSelection(context));
        pipelineStatements.AddRange(AddFinalWrite(context));
        return pipelineStatements;
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
    /// A statement that assigns the computed index to a variable named <see cref="CacheGenContext.IndexName"/>.
    /// Uses a specialized fast modulo when possible.
    /// </returns>
    /// <remarks>
    /// For <c>CacheBits == 16</c> the index is an unchecked cast; otherwise a 32×32→64 fast-mod pattern is inlined.
    /// </remarks>
    protected virtual IEnumerable<StatementSyntax> AddIndexSelector(CacheGenContext context)
    {
        var indexSelector = IndexSelectorFactory.Create(context);

        foreach(var statement in indexSelector.CreateIndexStatement(context))
        {
            yield return statement;
        }
    }

    /// <summary>
    /// Emits code that loads the target bucket entry by reference.
    /// </summary>
    /// <param name="context">Generation context providing the cache field and the local entry variable name.</param>
    /// <returns>
    /// A single statement assigning <c>ref</c> to <see cref="CacheGenContext.EntryLocal"/> from <see cref="CacheGenContext.CacheFieldName"/> at <see cref="CacheGenContext.IndexName"/>.
    /// </returns>
    protected virtual IEnumerable<StatementSyntax> AddEntryReference(CacheGenContext context)
    {
        yield return ParseStatement($"ref var {context.EntryLocal} = ref {context.CacheFieldName}[{context.IndexName}];");
    }

    /// <summary>
    /// Emits a linear probe over all ways and, on empty, replaces the entire entry via simple assignment.
    /// </summary>
    /// <param name="context">Generation context providing associativity and eviction policy.</param>
    /// <returns>
    /// For each way, generates a fast-path that:
    /// copies the current entry to a local (<c>newEntry</c>), sets value/stamp/hash for that way, assigns
    /// <c>entry = newEntry;</c>, and returns.
    /// </returns>
    /// <remarks>
    /// An empty slot is identified when the stored hash is zero or the stored value is <c>null</c>.
    /// When <see cref="SoftCacheEvictionPolicy.LruApprox"/> is used, the access stamp is updated using
    /// <see cref="System.Environment.TickCount"/>.
    /// </remarks>
    protected virtual IEnumerable<StatementSyntax> AddEmptySlotProbe(CacheGenContext context)
    {
        var associativity = context.Options.Associativity;

        for (var i = 0; i < associativity; i++)
        {
            var setStamp = context.Options.Eviction == SoftCacheEvictionPolicy.LruApprox
                ? $"newEntry.s{i} = global::System.Environment.TickCount;"
                : string.Empty;

            yield return ParseStatement(
                "if (entry.h" + i + " == 0 || entry.v" + i + " is null) " +
                "{ var newEntry = entry; newEntry.v" + i + " = value; " + setStamp + " newEntry.h" + i + " = hash; " +
                "entry = newEntry; return; }");
        }
    }

    /// <summary>
    /// Emits victim selection when the bucket is full.
    /// </summary>
    /// <param name="context">Generation context providing associativity and eviction policy.</param>
    /// <returns>
    /// For direct-mapped caches (associativity = 1), yields nothing.
    /// For <see cref="SoftCacheEvictionPolicy.Overwrite"/>, initializes <see cref="CacheGenContext.VictimIndexLocal"/> to 0.
    /// For <see cref="SoftCacheEvictionPolicy.LruApprox"/>, selects the way with the smallest stamp.
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
    /// Emits the final write by rebuilding and assigning the entire entry at once.
    /// </summary>
    /// <param name="context">Generation context providing associativity and local names.</param>
    /// <returns>
    /// For direct-mapped caches, defers to <see cref="AddFinalWriteDirectMapped"/>.
    /// For set-associative caches, defers to <see cref="AddFinalWriteSetAssociative"/>.
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
    /// Direct-mapped final write: copy entry, mutate way 0, assign entire entry back, return.
    /// </summary>
    /// <param name="context">Generation context.</param>
    /// <returns>
    /// Statements that assign value, optionally update the stamp, assign hash, then <c>entry = newEntry;</c> and return.
    /// </returns>
    protected virtual IEnumerable<StatementSyntax> AddFinalWriteDirectMapped(CacheGenContext context)
    {
        yield return ParseStatement("var newEntry = entry;");
        yield return ParseStatement("newEntry.v0 = value;");

        foreach (var stamp in BuildStampAssignment("newEntry.s0", context))
        {
            yield return stamp;
        }

        yield return ParseStatement("newEntry.h0 = hash;");
        yield return ParseStatement("entry = newEntry;");
        yield return ParseStatement("return;");
    }

    /// <summary>
    /// Set-associative final write: switch on victim index, mutate chosen way in a local copy, assign back, return.
    /// </summary>
    /// <param name="context">Generation context.</param>
    /// <returns>
    /// A <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.SwitchStatementSyntax"/> that writes to the selected way,
    /// assigns <c>entry = newEntry;</c>, and returns.
    /// </returns>
    protected virtual IEnumerable<StatementSyntax> AddFinalWriteSetAssociative(CacheGenContext context)
    {
        var sections = new List<SwitchSectionSyntax>();

        for (var i = 0; i < context.Options.Associativity; i++)
        {
            sections.Add(BuildSwitchSectionForWay(context, i));
        }

        sections.Add(BuildDefaultSwitchSection(context));

        yield return SwitchStatement(IdentifierName(context.VictimIndexLocal)).WithSections(List(sections));
    }

    /// <summary>
    /// Builds a switch section that replaces the entire entry after mutating the specified way.
    /// </summary>
    /// <param name="context">Generation context.</param>
    /// <param name="way">The way (index) to write.</param>
    /// <returns>
    /// A <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.SwitchSectionSyntax"/> that assigns value, optionally updates the stamp,
    /// assigns hash, performs <c>entry = newEntry;</c>, and returns.
    /// </returns>
    protected virtual SwitchSectionSyntax BuildSwitchSectionForWay(CacheGenContext context, int way)
    {
        var statements = new List<StatementSyntax>
        {
            ParseStatement("var newEntry = entry;"),
            ParseStatement($"newEntry.v{way} = value;")
        };

        foreach (var stamp in BuildStampAssignment($"newEntry.s{way}", context))
        {
            statements.Add(stamp);
        }

        statements.Add(ParseStatement($"newEntry.h{way} = hash;"));
        statements.Add(ParseStatement("entry = newEntry;"));
        statements.Add(ParseStatement("return;"));

        return SwitchSection()
            .WithLabels(SingletonList<SwitchLabelSyntax>(
                CaseSwitchLabel(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(way)))))
            .WithStatements(List(statements));
    }

    /// <summary>
    /// Default switch section: mutate way 0 in a local copy, assign entire entry back, return.
    /// </summary>
    /// <param name="context">Generation context.</param>
    /// <returns>
    /// A <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.SwitchSectionSyntax"/> that writes to way 0 and returns.
    /// </returns>
    protected virtual SwitchSectionSyntax BuildDefaultSwitchSection(CacheGenContext context)
    {
        var statements = new List<StatementSyntax>
        {
            ParseStatement("var newEntry = entry;"),
            ParseStatement("newEntry.v0 = value;")
        };

        foreach (var stamp in BuildStampAssignment("newEntry.s0", context))
        {
            statements.Add(stamp);
        }

        statements.Add(ParseStatement("newEntry.h0 = hash;"));
        statements.Add(ParseStatement("entry = newEntry;"));
        statements.Add(ParseStatement("return;"));

        return SwitchSection()
            .WithLabels(SingletonList<SwitchLabelSyntax>(DefaultSwitchLabel()))
            .WithStatements(List(statements));
    }

    /// <summary>
    /// Conditionally emits an assignment to the access timestamp for LRU-approx eviction.
    /// </summary>
    /// <param name="left">The left-hand-side expression that represents the stamp field (e.g., <c>newEntry.s0</c>).</param>
    /// <param name="context">Generation context providing the eviction policy.</param>
    /// <returns>
    /// A single assignment to <paramref name="left"/> when <see cref="SoftCacheEvictionPolicy.LruApprox"/> is enabled; otherwise yields nothing.
    /// </returns>
    /// <remarks>
    /// Uses <see cref="System.Environment.TickCount"/> as a coarse-grained timestamp suitable for approximate LRU.
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