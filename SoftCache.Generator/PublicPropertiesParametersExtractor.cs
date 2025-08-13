using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace SoftCache.Generator;

/// <summary>
/// Parameters extractor that uses only public readable instance properties,
/// skipping any property marked with <c>IgnoreCache</c> attribute.
/// </summary>
public sealed class PublicPropertiesParametersExtractor : IParametersExtractor
{
    public ImmutableArray<ExtractedParameter> ExtractParameters(ITypeSymbol typeSymbol)
    {
        // Collect from the type and its base types (public instance properties)
        var builder = ImmutableArray.CreateBuilder<ExtractedParameter>();

        foreach (var property in EnumeratePublicReadableInstanceProperties(typeSymbol))
        {
            if (HasIgnoreCacheAttribute(property))
            {
                continue;
            }

            // Name = property name; Type = property.Type (still an ISymbol for your record)
            builder.Add(new ExtractedParameter(
                RawSymbol: property,
                Name: property.Name,
                Type: property.Type));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Enumerates public, readable, instance properties from the type and its base types.
    /// Excludes indexers and static properties.
    /// </summary>
    private static IEnumerable<IPropertySymbol> EnumeratePublicReadableInstanceProperties(ITypeSymbol type)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            foreach (var member in t.GetMembers().OfType<IPropertySymbol>())
            {
                if (member.IsIndexer)
                {
                    continue;
                }

                if (member.IsStatic)
                {
                    continue;
                }

                if (member.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                // Must be readable (public get)
                var getter = member.GetMethod;
                if (getter is null || getter.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                yield return member;
            }
        }
    }

    /// <summary>
    /// Checks if a symbol has an IgnoreCache attribute (by simple name or full name).
    /// </summary>
    private static bool HasIgnoreCacheAttribute(ISymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            var @class = attr.AttributeClass;
            if (@class is null)
            {
                continue;
            }

            // Match by short name or by full name suffix to be resilient to namespace differences
            var name = @class.Name; // e.g., "IgnoreCacheAttribute"
            if (name is "IgnoreCacheAttribute" or "IgnoreCache")
            {
                return true;
            }

            var full = @class.ToDisplayString(); // e.g., "SoftCache.IgnoreCacheAttribute"
            if (full.EndsWith(".IgnoreCacheAttribute", StringComparison.Ordinal) ||
                full.EndsWith(".IgnoreCache", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}