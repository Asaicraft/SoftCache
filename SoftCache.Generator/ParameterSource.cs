namespace SoftCache.Generator;

/// <summary>
/// Indicates where the parameter was extracted from.
/// </summary>
public enum ParameterSource
{
    /// <summary>
    /// Parameter was extracted from a public property.
    /// </summary>
    Property,

    /// <summary>
    /// Parameter was extracted from a constructor parameter.
    /// </summary>
    Constructor
}