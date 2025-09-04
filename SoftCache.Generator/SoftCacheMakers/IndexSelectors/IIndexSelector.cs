using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator.SoftCacheMakers.IndexSelectors;

public interface IIndexSelector
{
    public StatementSyntax CreateIndexStatement(CacheGenContext context);
}
