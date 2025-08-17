using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator.SoftCacheMaker;
public interface ISoftCacheMaker
{
    public ClassDeclarationSyntax CreateSoftCache(SoftCacheOptions softCacheOptions, bool isInternalElsePublic = true);
}
