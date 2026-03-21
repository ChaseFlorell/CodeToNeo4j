using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.Technologies.DotNet.CSharp;

public interface IAccessibilityFilter
{
	bool IsAccessibilityBelowMinimum(ISymbol symbol, Accessibility minAccessibility);

	bool IsExplicitInterfaceImplementation(ISymbol symbol);
}

public class AccessibilityFilter : IAccessibilityFilter
{
	public bool IsAccessibilityBelowMinimum(ISymbol symbol, Accessibility minAccessibility) =>
		symbol.DeclaredAccessibility < minAccessibility
		&& symbol.DeclaredAccessibility != Accessibility.NotApplicable
		&& !IsExplicitInterfaceImplementation(symbol);

	public bool IsExplicitInterfaceImplementation(ISymbol symbol) =>
		symbol switch
		{
			IMethodSymbol method => method.ExplicitInterfaceImplementations.Any(),
			IPropertySymbol property => property.ExplicitInterfaceImplementations.Any(),
			IEventSymbol @event => @event.ExplicitInterfaceImplementations.Any(),
			_ => false
		};
}
