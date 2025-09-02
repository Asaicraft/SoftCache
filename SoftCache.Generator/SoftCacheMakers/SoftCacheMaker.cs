using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using SoftCache.Generator.SoftCacheMakers.WritePolicyMakers;
using SoftCache.Annotations;

namespace SoftCache.Generator.SoftCacheMakers;
public sealed class SoftCacheMaker: ISoftCacheMaker
{
    public static ISoftCacheMaker Instance = new SoftCacheMaker();

    public ClassDeclarationSyntax CreateSoftCache(SoftCacheOptions softCacheOptions, bool isInternalElsePublic = true)
    {
        // Validate target type
        var namedTarget = softCacheOptions.TargetType as INamedTypeSymbol
            ?? throw new ArgumentException("SoftCacheOptions.TargetType must be INamedTypeSymbol", nameof(softCacheOptions));

        // Fully-qualified "global::Ns.Type"
        var fqType = namedTarget.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat
                .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included));

        // "global::Ns.Type.Parameters"
        var parametersType = fqType + ".Parameters";

        var cacheSize = softCacheOptions.UseNearestPrime 
            ? (1 << softCacheOptions.CacheBits)
            : NearestPrimes.For(softCacheOptions.CacheBits);

        // Consistent identifiers used across the generator
        const string cacheClassName = "SoftCache";
        const string cacheMaskName = "CacheMask";
        const string entryStructName = "Entry";
        const string cacheFieldName = "s_cache";
        const string stampFieldName = "s_stamp";
        const string statsName = "SoftCacheStats";
        const string entryLocal = "entry";
        const string entryValueLocal = "slotValue";
        const string entryHashLocal = "slotHash";
        const string entryStampLocal = "slotStamp";
        const string victimIndexLocal = "victimIndex";
        const string indexName = "index";
        const string lockFieldName = "s_lockObject";

        var genContext = new CacheGenContext(
            Options: softCacheOptions,
            TargetType: namedTarget,
            CacheSize: cacheSize,
            IsInternalElsePublic: isInternalElsePublic,
            FullyQualifiedTypeName: fqType,
            ParametersTypeName: parametersType,
            CacheClassName: cacheClassName,
            CacheMaskName: cacheMaskName,
            EntryStructName: entryStructName,
            CacheFieldName: cacheFieldName,
            EntryLocal: entryLocal,
            EntryValueLocal: entryValueLocal,
            EntryHashLocal: entryHashLocal,
            EntryStampLocal: entryStampLocal,
            VictimIndexLocal: victimIndexLocal,
            StampFieldName: stampFieldName,
            StatsName: statsName,
            IndexName: indexName,
            LockFieldName: softCacheOptions.Concurrency == SoftCacheConcurrency.Lock 
                ? lockFieldName
                : null
        );

        // TODO: use genContext to emit the whole class (fields, ctor, Add method, etc.)
        // For now return the class shell with proper visibility; the rest of generation uses genContext.
        var visibility = isInternalElsePublic ? SyntaxKind.InternalKeyword : SyntaxKind.PublicKeyword;

        var list = new List<MemberDeclarationSyntax>();

        var addCacheMethod = WritePolicyMaker.CreateWritePolicyMaker(genContext).CreateWriter(genContext);
        list.Add(addCacheMethod);

        list.AddRange(CreateLayout(genContext));

        return ClassDeclaration(cacheClassName)
            .WithModifiers(TokenList(Token(visibility), Token(SyntaxKind.SealedKeyword), Token(SyntaxKind.PartialKeyword)))
            .WithMembers(List(list));
    }

    private IEnumerable<MemberDeclarationSyntax> CreateLayout(CacheGenContext genContext)
    {
        if(genContext.LockFieldName != null)
        {
            // private static readonly object s_lockObject = new object();
            // or
            // private static readonly global::System.Threading.Lock s_lockObject = global::System.Threading.Lock();

            if(!genContext.Options.HasSystemThreadingLock)
            {
                yield return FieldDeclaration(
                    VariableDeclaration(
                        PredefinedType(Token(SyntaxKind.ObjectKeyword)))
                    .WithVariables(
                        SingletonSeparatedList(
                            VariableDeclarator(Identifier(genContext.LockFieldName))
                            .WithInitializer(
                                EqualsValueClause(
                                    ObjectCreationExpression(
                                        PredefinedType(Token(SyntaxKind.ObjectKeyword)))
                                    .WithArgumentList(ArgumentList()))))))
                .WithModifiers(TokenList(
                    Token(SyntaxKind.PrivateKeyword),
                    Token(SyntaxKind.StaticKeyword),
                    Token(SyntaxKind.ReadOnlyKeyword)));
            }
            else
            {
                yield return FieldDeclaration(
                    VariableDeclaration(
                        IdentifierName("global::System.Threading.Lock"))
                    .WithVariables(
                        SingletonSeparatedList(
                            VariableDeclarator(Identifier(genContext.LockFieldName))
                            .WithInitializer(
                                EqualsValueClause(
                                    ObjectCreationExpression(
                                        IdentifierName("global::System.Threading.Lock"))
                                    .WithArgumentList(ArgumentList()))))))
                .WithModifiers(TokenList(
                    Token(SyntaxKind.PrivateKeyword),
                    Token(SyntaxKind.StaticKeyword),
                    Token(SyntaxKind.ReadOnlyKeyword)));
            }
            
        }
    }
}
