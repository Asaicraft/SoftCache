using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using System.Diagnostics;

namespace SoftCache.Generator.SoftCacheMakers.WritePolicyMakers;

/// <summary>
/// Lock-based writer: wraps the whole Add pipeline into a single monitor lock.
/// </summary>
public sealed class LockWritePolicyMaker : WritePolicyMaker
{
    public static readonly LockWritePolicyMaker Instance = new();

    protected override IEnumerable<StatementSyntax> BuildPipelineStatements(CacheGenContext context)
    {
        Debug.Assert(context.LockFieldName != null);

        yield return LockStatement(
            IdentifierName(context.LockFieldName!), 
            Block(base.BuildPipelineStatements(context))
        );
    }
}