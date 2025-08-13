using Microsoft.CodeAnalysis;
using System;

namespace SoftCache.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class SoftCacheGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        throw new NotImplementedException();
    }
}
