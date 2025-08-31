using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Text;
using SoftCache.Annotations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SoftCache.Generator.SoftCacheMaker.WritePolicyMakers;

/// <summary>
/// Base “template method” for generating Add(value, hash):
/// - compute index;
/// - load entry;
/// - probe for an empty slot (if enabled);
/// - choose a victim when the bucket is full;
/// - perform the write (with optional wrappers for CAS policies).
/// Derivations override hook(s) without rewriting the whole method.
/// </summary>
public abstract class BaseWritePolicyMaker : IWritePolicyMaker
{
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

    protected virtual IEnumerable<StatementSyntax> AddDebugInfoIfNeeded(CacheGenContext context)
    {
        if (context.Options.EnableDebugMetrics)
        {
            yield return ParseStatement($"{context.FullyQualifiedTypeName}.{context.StatsName}.ItemAdded();");
        }
    }

    /// <summary>
    /// Should return a variable with name <see cref="CacheGenContext.IndexName"/>
    /// </summary>
    protected virtual IEnumerable<StatementSyntax> AddIndexSelector(CacheGenContext context)
    {
        var expression = context.Options.CacheBits == 16
            ? "hash"
            : $"hash & {context.CacheMaskName}";

        yield return ParseStatement($"var {context.IndexName} = {expression};");
    }

    protected virtual IEnumerable<StatementSyntax> AddEntryReference(CacheGenContext context)
    {
        yield return ParseStatement($"ref var {context.EntryLocal} = ref {context.CacheFieldName}[{context.IndexName}];");
    }

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

    protected virtual IEnumerable<StatementSyntax> BuildStampAssignment(string left, CacheGenContext context)
    {
        if (context.Options.Eviction != SoftCacheEvictionPolicy.LruApprox)
        {
            yield break;
        }

        yield return ParseStatement($"{left} = global::System.Environment.TickCount;");
    }
}