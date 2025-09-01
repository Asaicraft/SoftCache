using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SoftCache.Annotations;
using SoftCache.Generator.SoftCacheMakers;
using SoftCache.Generator.StaticHashMakers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SoftCache.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class SoftCacheGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "SoftCache.Annotations.SoftCacheAttribute";

    public static readonly IParametersExtractor ParametersExtractor = new PublicPropertiesParametersExtractor();
    public static readonly IParametersMaker ParametersMaker = new ParametersMaker();
    public static readonly IGetParametersMaker GetParametersMaker = new GetParametersMaker();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var optionsProvider =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: AttributeMetadataName,
                    predicate: static (_, _) => true, // Roslyn filters by attribute name
                    transform: static (transformContext, _) =>
                    {
                        var targetTypeSymbol = (ITypeSymbol)transformContext.TargetSymbol;

                        // Ensure this is exactly our SoftCache.Annotations attribute (not just a namesake)
                        var expectedAttribute =
                            transformContext.SemanticModel.Compilation.GetTypeByMetadataName(AttributeMetadataName);
                        if (expectedAttribute is null)
                        {
                            return (ok: false, options: default(SoftCacheOptions));
                        }

                        var attributeData = transformContext.Attributes
                            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, expectedAttribute));

                        if (attributeData is null)
                        {
                            return (ok: false, options: default(SoftCacheOptions));
                        }

                        var parsedOptions = ParseOptions(attributeData, targetTypeSymbol);
                        return (ok: true, options: parsedOptions);
                    })
                .Where(static tuple => tuple.ok)
                .Select(static (tuple, _) => tuple.options);

        context.RegisterSourceOutput(optionsProvider, static (spc, options) =>
        {
            if (options?.TargetType is not INamedTypeSymbol target)
            {
                return;
            }

            var extractedParameters = ParametersExtractor.ExtractParameters(target);

            var parametersText = CreateParametersText(target, extractedParameters);
            var getParametersText = CreateGetParametersText(target, extractedParameters);
            var softEqualsText = CreateSoftEqualsText(target, extractedParameters);
            var softHashText = CreateSoftHashBundleText(target, options, extractedParameters);
            var softCache = CreateSoftCacheClass(target, options, extractedParameters);

            spc.AddSource(parametersText);
            spc.AddSource(getParametersText);
            spc.AddSource(softEqualsText);
            spc.AddSource(softHashText);
            spc.AddSource(softCache);
        });
    }

    private static GeneratedSourceFile CreateParametersText(INamedTypeSymbol target, ImmutableArray<ExtractedParameter> extractedParameters)
    {
        var fullTypeName = target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat); // global::A.B.C
        var safeName = CreateFileName(fullTypeName, "Parameters");

        // 1) Build nested struct Parameters
        var parametersStruct = ParametersMaker.MakeParameters(target, extractedParameters);

        // 2) Wrap partial type with nested struct
        var partialWithParameters = BuildPartialContainer(target, [parametersStruct]);
        var compilationUnitWithParameters = CompilationUnit()
            .WithMembers(SingletonList(partialWithParameters))
            .NormalizeWhitespace();

        return (compilationUnitWithParameters.GetText(Encoding.UTF8), safeName);
    }

    private static GeneratedSourceFile CreateGetParametersText(INamedTypeSymbol target, ImmutableArray<ExtractedParameter> extractedParameters)
    {
        var fullTypeName = target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat); // global::A.B.C
        var safeName = CreateFileName(fullTypeName, "GetParameters");

        // 1) Build explicit interface implementation GetParameters()
        var getParametersMethod = GetParametersMaker.CreateGetParameters(target, extractedParameters);

        var baseList = BaseList(SeparatedList<BaseTypeSyntax>([
             SimpleBaseType(ParseTypeName($"global::SoftCache.ISoftCacheable<{fullTypeName}.Parameters>"))
         ]));

        // 2) Wrap partial type with the method GetParameters
        var partialWithMethod = BuildPartialContainer(target, [getParametersMethod], baseList);
        var compilationUnitWithMethod = CompilationUnit()
            .WithMembers(SingletonList(partialWithMethod))
            .NormalizeWhitespace();

        return (compilationUnitWithMethod.GetText(Encoding.UTF8), safeName);
    }

    private static GeneratedSourceFile CreateSoftHashBundleText(INamedTypeSymbol target, SoftCacheOptions options, ImmutableArray<ExtractedParameter> extractedParameters)
    {
        // Fully qualified type name and ISoftCacheable<T>
        var fullyQualifiedName = target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat); // global::Ns.Type
        var interfaceFullyQualifiedName = $"global::SoftCache.ISoftCacheable<{fullyQualifiedName}.Parameters>";

        // 1) Explicit GetSoftHashCode method
        var explicitInterface = ExplicitInterfaceSpecifier(ParseName(interfaceFullyQualifiedName));

        // var parameters = ((global::SoftCache.ISoftCacheable<...>)this).GetParameters();
        var getParametersStatement = ParseStatement(
            $"var parameters = (({interfaceFullyQualifiedName})this).GetParameters();");

        // return MakeSoftHash(in parameters);
        var returnStatement = ReturnStatement(
            InvocationExpression(IdentifierName("MakeSoftHash"))
                .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList(
                            Argument(
                                nameColon: null,
                                refKindKeyword: Token(SyntaxKind.InKeyword),
                                expression: IdentifierName("parameters"))))));

        var getSoftHashCodeMethod =
            MethodDeclaration(PredefinedType(Token(SyntaxKind.UShortKeyword)), Identifier("GetSoftHashCode"))
                .WithExplicitInterfaceSpecifier(explicitInterface)
                .WithBody(Block(getParametersStatement, returnStatement))
                .WithLeadingTrivia(ParseLeadingTrivia("""
                /// <inheritdoc cref="global::SoftCache.ISoftCacheable{T}.GetSoftHashCode"/>
                
                """));

        // 2) Static MakeSoftHash(...) — choose implementation by SoftHashKind
        var staticHashMaker = StaticHashMaker.GetStaticHashMaker(options);
        var makeSoftHashMethod = staticHashMaker.MakeStatic(options, target, extractedParameters);

        // 3) Add interface to the base list
        var baseList = BaseList(
            SingletonSeparatedList<BaseTypeSyntax>(
                SimpleBaseType(ParseTypeName(interfaceFullyQualifiedName))));

        // 4) Wrap everything into a partial of the target type (respecting namespace and nested types)
        var partialDeclaration = BuildPartialContainer(
            target,
            [getSoftHashCodeMethod, makeSoftHashMethod],
            baseListSyntax: baseList);

        var compilationUnit = CompilationUnit()
            .WithMembers(SingletonList(partialDeclaration))
            .NormalizeWhitespace();

        return (compilationUnit.GetText(Encoding.UTF8), CreateFileName(fullyQualifiedName, "SoftHash"));
    }

    private static GeneratedSourceFile CreateSoftEqualsText(
        INamedTypeSymbol target,
        ImmutableArray<ExtractedParameter> extractedParameters)
    {
        var fullyQualifiedTypeName = target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var safeFileName = CreateFileName(fullyQualifiedTypeName, "SoftEquals");

        // Build a chain: this.X == parameters.X
        ExpressionSyntax? combinedCondition = null;

        foreach (var parameter in extractedParameters)
        {
            var comparisonExpression = BinaryExpression(
                SyntaxKind.EqualsExpression,
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ThisExpression(),
                    IdentifierName(parameter.Name)),
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("parameters"),
                    IdentifierName(parameter.Name)));

            combinedCondition = combinedCondition is null
                ? comparisonExpression
                : BinaryExpression(SyntaxKind.LogicalAndExpression, combinedCondition, comparisonExpression);
        }

        // If there are no parameters — always true
        var returnStatement = ReturnStatement(combinedCondition ?? LiteralExpression(SyntaxKind.TrueLiteralExpression));

        var xmlDocumentation = ParseLeadingTrivia($$"""
        /// <summary>
        /// Determines whether this object is considered equivalent to the given parameter set
        /// for the purposes of cache lookup.
        /// <para>
        /// This method performs a fast, field-by-field comparison of the parameters
        /// that define the object's identity.
        /// </para>
        /// </summary>
        /// <param name="parameters">
        /// The immutable parameters describing a candidate object to compare against this instance.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the parameter values match this instance's identity;
        /// otherwise, <see langword="false"/>.
        /// </returns>

        """);

        var parametersType = ParseTypeName($"{fullyQualifiedTypeName}.Parameters");

        var methodDeclaration = MethodDeclaration(
                PredefinedType(Token(SyntaxKind.BoolKeyword)),
                Identifier("SoftEquals"))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(
                ParameterList(
                    SingletonSeparatedList(
                        Parameter(Identifier("parameters"))
                            .WithModifiers(TokenList(Token(SyntaxKind.ScopedKeyword), Token(SyntaxKind.InKeyword)))
                            .WithType(parametersType))))
            .WithBody(Block(returnStatement))
            .WithLeadingTrivia(xmlDocumentation);

        var partialContainer = BuildPartialContainer(target, new[] { methodDeclaration });

        var compilationUnit = CompilationUnit()
            .WithMembers(SingletonList<MemberDeclarationSyntax>(partialContainer))
            .NormalizeWhitespace();

        return (compilationUnit.GetText(Encoding.UTF8), safeFileName);
    }

    private static GeneratedSourceFile CreateSoftCacheClass(
        INamedTypeSymbol target,
        SoftCacheOptions options,
        ImmutableArray<ExtractedParameter> extractedParameters)
    {
        var generator = SoftCacheMaker.Instance;

        // The generated cache class (ClassDeclarationSyntax), e.g. "sealed partial class SoftCache ..."
        var generatedCacheClass = generator.CreateSoftCache(options);

        // Wrap in a partial container of the target type:
        // namespace <Namespace> { partial class/struct <Target> { <generatedCacheClass> } }
        var partialContainer = BuildPartialContainer(
            target,
            [generatedCacheClass]);

        var compilationUnit = CompilationUnit()
            .WithMembers(SingletonList(partialContainer))
            .NormalizeWhitespace();

        // File name like A_B_C.SoftCache.g.cs (consistent with other generations)
        var fullyQualifiedTypeName = target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var fileName = CreateFileName(fullyQualifiedTypeName, "SoftCache");

        return (compilationUnit.GetText(Encoding.UTF8), fileName);
    }



    // ---------- helpers ----------

    // Builds a partial wrapper for the target type and inserts members inside (respecting namespace/nested type hierarchy).
    // Builds a partial wrapper for the target type and inserts members inside
    // (respecting namespace/nested type hierarchy). Optionally applies a base list
    // (interfaces/base type) to the *target* (innermost) partial declaration.
    private static MemberDeclarationSyntax BuildPartialContainer(
        INamedTypeSymbol targetType,
        IEnumerable<MemberDeclarationSyntax> members,
        BaseListSyntax? baseListSyntax = null)
    {
        // 1) Collect the chain of nested types from outermost to innermost
        var typeChain = new Stack<INamedTypeSymbol>();
        for (var typeCursor = targetType; typeCursor is not null; typeCursor = typeCursor.ContainingType)
        {
            typeChain.Push(typeCursor);
        }

        // 2) Create partial-type headers for each element in the chain
        var declarations = new List<TypeDeclarationSyntax>(typeChain.Count);
        foreach (var symbol in typeChain)
        {
            declarations.Add(CreatePartialTypeHeader(symbol));
        }

        // 3) Apply the base list (if provided) to the target (innermost) type
        var innermostIndex = declarations.Count - 1;
        if (baseListSyntax is not null)
        {
            declarations[innermostIndex] = declarations[innermostIndex].WithBaseList(baseListSyntax);
        }

        // 4) Insert the provided members into the target (innermost) type
        var current = declarations[innermostIndex]
            .WithMembers(declarations[innermostIndex].Members.AddRange(members));

        // 5) Wrap the innermost type with its outer containers:
        //    …Outer { …Inner { Target { members } } }
        for (var i = innermostIndex - 1; i >= 0; i--)
        {
            current = declarations[i].WithMembers(SingletonList<MemberDeclarationSyntax>(current));
        }

        // 6) Wrap with the namespace if present
        var containingNamespace = targetType.ContainingNamespace;
        if (containingNamespace is { IsGlobalNamespace: false })
        {
            return NamespaceDeclaration(ParseName(containingNamespace.ToDisplayString()))
                .WithMembers(SingletonList<MemberDeclarationSyntax>(current));
        }

        return current;
    }


    private static TypeDeclarationSyntax CreatePartialTypeHeader(INamedTypeSymbol type)
    {
        // Determine kind
        var kind = type.TypeKind switch
        {
            TypeKind.Struct => SyntaxKind.StructDeclaration,
            _ => SyntaxKind.ClassDeclaration
        };

        // Identifier + generic parameters
        var id = Identifier(type.Name);

        var declaration = TypeDeclaration(kind, id)
            .WithModifiers(TokenList(Token(SyntaxKind.PartialKeyword)));

        // Generic parameters, if any
        if (type.TypeParameters.Length > 0)
        {
            var typeParameters = TypeParameterList(
                SeparatedList(type.TypeParameters.Select(tp => TypeParameter(tp.Name))));
            declaration = declaration.WithTypeParameterList(typeParameters);

            // Generic constraints (required for each partial part)
            if (type is INamedTypeSymbol namedType && namedType.TypeParameters.Length > 0)
            {
                var constraintClauses = new List<TypeParameterConstraintClauseSyntax>();
                foreach (var typeParameter in namedType.TypeParameters)
                {
                    var constraints = new List<TypeParameterConstraintSyntax>();

                    if (typeParameter.HasReferenceTypeConstraint)
                    {
                        constraints.Add(ClassOrStructConstraint(SyntaxKind.ClassConstraint));
                    }

                    if (typeParameter.HasUnmanagedTypeConstraint)
                    {
                        constraints.Add(TypeConstraint(ParseTypeName("unmanaged")));
                    }

                    if (typeParameter.HasNotNullConstraint)
                    {
                        constraints.Add(TypeConstraint(ParseTypeName("notnull")));
                    }

                    if (typeParameter.HasValueTypeConstraint)
                    {
                        constraints.Add(ClassOrStructConstraint(SyntaxKind.StructConstraint));
                    }

                    foreach (var ct in typeParameter.ConstraintTypes)
                    {
                        constraints.Add(TypeConstraint(ParseTypeName(ct.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))));
                    }

                    if (typeParameter.HasConstructorConstraint)
                    {
                        constraints.Add(ConstructorConstraint());
                    }

                    if (constraints.Count > 0)
                    {
                        constraintClauses.Add(
                            TypeParameterConstraintClause(IdentifierName(typeParameter.Name))
                                .WithConstraints(SeparatedList(constraints)));
                    }
                }

                if (constraintClauses.Count > 0)
                {
                    declaration = declaration.WithConstraintClauses(List(constraintClauses));
                }
            }
        }

        // (Optionally) replicate attributes/access modifiers — not required for a partial part
        return declaration;
    }

    private static string CreateFileName(string fullyQualifiedName, string name) => $"{SafeFileMoniker(fullyQualifiedName)}.{name}.g.cs";
    private static string SafeFileMoniker(string fullyQualifiedName)
    {
        // "global::A.B.C" → "A_B_C"
        var withoutGlobal = fullyQualifiedName.StartsWith("global::", System.StringComparison.Ordinal)
            ? fullyQualifiedName.Substring(8)
            : fullyQualifiedName;
        var chars = withoutGlobal.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return new string(chars);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    public static string SeedIdentifier(SoftCacheOptions options, string fullyQualifiedType)
    {
        return $"{fullyQualifiedType}.SoftCache.s_seed";
    }

    public static string Sanitize(string identifier)
    {
        var builder = new System.Text.StringBuilder(identifier.Length);
        foreach (var ch in identifier)
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }
        return builder.ToString();
    }

    private static SoftCacheOptions ParseOptions(AttributeData attributeData, ITypeSymbol targetType)
    {
        var cacheBits = 16;
        var associativity = 1;
        var hashKind = SoftHashKind.XorFold16;
        var concurrency = SoftCacheConcurrency.None;
        var generateSeed = false;
        var enableDebugMetrics = false;
        var eviction = SoftCacheEvictionPolicy.Overwrite;

        foreach (var namedArgument in attributeData.NamedArguments)
        {
            var name = namedArgument.Key;
            var typedConstant = namedArgument.Value;

            switch (name)
            {
                case nameof(SoftCacheOptions.CacheBits):
                {
                    if (typedConstant.Value is int cacheBitsValue)
                    {
                        cacheBits = cacheBitsValue;
                    }
                    break;
                }

                case nameof(SoftCacheOptions.Associativity):
                {
                    if (typedConstant.Value is int associativityValue)
                    {
                        associativity = associativityValue;
                    }
                    break;
                }

                case nameof(SoftCacheOptions.HashKind):
                {
                    if (typedConstant.Value is int hashKindInt)
                    {
                        hashKind = (SoftHashKind)hashKindInt;
                    }
                    else if (typedConstant.Type?.TypeKind == TypeKind.Enum && typedConstant.Value is object hashKindObj)
                    {
                        hashKind = (SoftHashKind)System.Convert.ToInt32(hashKindObj);
                    }
                    break;
                }

                case nameof(SoftCacheOptions.Concurrency):
                {
                    if (typedConstant.Value is int concurrencyInt)
                    {
                        concurrency = (SoftCacheConcurrency)concurrencyInt;
                    }
                    else if (typedConstant.Type?.TypeKind == TypeKind.Enum && typedConstant.Value is object concurrencyObj)
                    {
                        concurrency = (SoftCacheConcurrency)System.Convert.ToInt32(concurrencyObj);
                    }
                    break;
                }

                case nameof(SoftCacheOptions.GenerateGlobalSeed):
                {
                    if (typedConstant.Value is bool seed)
                    {
                        generateSeed = seed;
                    }
                    break;
                }

                case nameof(SoftCacheOptions.EnableDebugMetrics):
                {
                    if (typedConstant.Value is bool metrics)
                    {
                        enableDebugMetrics = metrics;
                    }
                    break;
                }

                case nameof(SoftCacheOptions.Eviction):
                {
                    if (typedConstant.Value is int evictionInt)
                    {
                        eviction = (SoftCacheEvictionPolicy)evictionInt;
                    }
                    else if (typedConstant.Type?.TypeKind == TypeKind.Enum && typedConstant.Value is object evictionObj)
                    {
                        eviction = (SoftCacheEvictionPolicy)System.Convert.ToInt32(evictionObj);
                    }
                    break;
                }
            }
        }

        // Validation/normalization
        if (cacheBits is < 1 or > 16)
        {
            cacheBits = 16;
        }

        if (associativity < 1)
        {
            associativity = 1;
        }

        if (associativity > 4)
        {
            associativity = 4;
        }

        return new SoftCacheOptions
        {
            CacheBits = cacheBits,
            Associativity = associativity,
            HashKind = hashKind,
            Concurrency = concurrency,
            Eviction = eviction,
            GenerateGlobalSeed = generateSeed,
            EnableDebugMetrics = enableDebugMetrics,
            TargetType = targetType
        };
    }
}
