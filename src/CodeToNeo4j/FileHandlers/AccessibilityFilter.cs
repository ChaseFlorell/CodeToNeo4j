using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

internal static class AccessibilityFilter
{
    internal static bool IsAccessibilityBelowMinimum(ISymbol symbol, Accessibility minAccessibility) =>
        symbol.DeclaredAccessibility < minAccessibility
        && symbol.DeclaredAccessibility != Accessibility.NotApplicable
        && !IsExplicitInterfaceImplementation(symbol);

    internal static bool IsExplicitInterfaceImplementation(ISymbol symbol) =>
        symbol switch
        {
            IMethodSymbol method => method.ExplicitInterfaceImplementations.Any(),
            IPropertySymbol property => property.ExplicitInterfaceImplementations.Any(),
            IEventSymbol @event => @event.ExplicitInterfaceImplementations.Any(),
            _ => false
        };
}
