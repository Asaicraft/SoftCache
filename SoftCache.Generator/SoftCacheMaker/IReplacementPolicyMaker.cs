using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator.SoftCacheMakers;
public interface IReplacementPolicyMaker
{
    public IEnumerable<MemberDeclarationSyntax> CreateReplacementHelpers(CacheGenContext context);

    public StatementSyntax CreateVictimSelectionStatement(CacheGenContext context);
}
