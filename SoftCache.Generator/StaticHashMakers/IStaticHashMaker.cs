using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace SoftCache.Generator.StaticHashMakers;

public interface IStaticHashMaker
{
    public MethodDeclarationSyntax MakeStatic(ITypeSymbol typeSymbol, ImmutableArray<ExtractedParameter> extractedParameters);
}
