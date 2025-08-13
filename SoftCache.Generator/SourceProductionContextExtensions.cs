using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator;
internal static class SourceProductionContextExtensions
{
    public static void AddSource(this SourceProductionContext context, GeneratedSourceFile generatedSourceFile)
    {
        context.AddSource(generatedSourceFile.FileName, generatedSourceFile.SourceText);
    }
}
