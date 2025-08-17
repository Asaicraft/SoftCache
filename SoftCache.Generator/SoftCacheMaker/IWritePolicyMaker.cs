using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator.SoftCacheMaker;

/// <summary>
/// Generate AddToCache(SoftCacheableObject cache, ushort hash)
/// </summary>
public interface IWritePolicyMaker
{
    public MethodDeclarationSyntax CreateWriter(CacheGenContext cacheGenContext);
}
