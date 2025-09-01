using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using SoftCache.Generator.SoftCacheMakers.WritePolicyMakers;

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

        // Compute actual cache size (2^CacheBits for now).
        // NOTE: CacheGenContext allows non-powers-of-two in the future; generator can change this later.
        var cacheSize = 1 << softCacheOptions.CacheBits;

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

        // For direct-mapped caches we frequently address .s0 / .h0 / .v0
        string? indexSuffix = softCacheOptions.Associativity == 1 ? "0" : null;

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
            IndexSuffix: indexSuffix
        );

        // TODO: use genContext to emit the whole class (fields, ctor, Add method, etc.)
        // For now return the class shell with proper visibility; the rest of generation uses genContext.
        var visibility = isInternalElsePublic ? SyntaxKind.InternalKeyword : SyntaxKind.PublicKeyword;

        var addCacheMethod = WritePolicyMaker.CreateWritePolicyMaker(genContext).CreateWriter(genContext);

        return ClassDeclaration(cacheClassName)
            .WithModifiers(TokenList(Token(visibility), Token(SyntaxKind.SealedKeyword), Token(SyntaxKind.PartialKeyword)))
            .WithMembers(SingletonList<MemberDeclarationSyntax>(addCacheMethod));
    }
}
