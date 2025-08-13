using Microsoft.CodeAnalysis.Text;

namespace SoftCache.Generator;

internal record struct GeneratedSourceFile(SourceText SourceText, string FileName)
{
    public static implicit operator (SourceText SourceText, string FileName)(GeneratedSourceFile value)
    {
        return (value.SourceText, value.FileName);
    }

    public static implicit operator GeneratedSourceFile((SourceText SourceText, string FileName) value)
    {
        return new GeneratedSourceFile(value.SourceText, value.FileName);
    }


}