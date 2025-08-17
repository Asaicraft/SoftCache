using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator.SoftCacheMaker;
public interface IReplacementPolicyMaker
{
    public IEnumerable<MemberDeclarationSyntax> CreateReplacementHelpers(CacheGenContext ctx);

    public StatementSyntax CreateVictimSelectionStatement(CacheGenContext ctx);
}
