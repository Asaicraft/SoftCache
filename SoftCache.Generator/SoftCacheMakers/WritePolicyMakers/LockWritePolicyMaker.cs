using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SoftCache.Generator.SoftCacheMakers.WritePolicyMakers;

/// <summary>
/// Lock-based writer: wraps the whole Add pipeline into a single monitor lock.
/// </summary>
public sealed class LockWritePolicyMaker : WritePolicyMaker
{
    public static readonly LockWritePolicyMaker Instance = new();

    public override MethodDeclarationSyntax CreateWriter(CacheGenContext context)
    {
        // Method signature: public static void Add({T} value, ushort hash)
        var method =
            MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier("Add"))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(
                ParameterList(SeparatedList(
                [
                        Parameter(Identifier("value"))
                            .WithType(ParseTypeName(context.FullyQualifiedTypeName)),
                        Parameter(Identifier("hash"))
                            .WithType(PredefinedType(Token(SyntaxKind.UIntKeyword)))
                ])));

        // Compose inner pipeline (same hooks/order as base) but wrap them in a lock
        var inner = new List<StatementSyntax>();
        inner.AddRange(AddDebugInfoIfNeeded(context));
        inner.AddRange(AddIndexSelector(context));
        inner.AddRange(AddEntryReference(context));
        inner.AddRange(AddEmptySlotProbe(context));
        inner.AddRange(AddVictimSelection(context));
        inner.AddRange(AddFinalWrite(context));

        var locked = LockStatement(
            IdentifierName(context.LockFieldName!),             // lock(s_lock)
            Block(inner));                                     // { ...full pipeline... }

        return method.WithBody(Block(locked));
    }

    private ExpressionSyntax IdentifierName(object lockFieldName)
    {
        throw new NotImplementedException();
    }
}