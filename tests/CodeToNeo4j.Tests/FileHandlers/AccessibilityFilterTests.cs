using CodeToNeo4j.FileHandlers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class AccessibilityFilterTests
{
    [Theory]
    [InlineData("public int Foo { get; set; }", Accessibility.Public, false)]
    [InlineData("private int Foo { get; set; }", Accessibility.Public, true)]
    [InlineData("internal int Foo { get; set; }", Accessibility.Public, true)]
    [InlineData("protected int Foo { get; set; }", Accessibility.Public, true)]
    [InlineData("private int Foo { get; set; }", Accessibility.Private, false)]
    public void GivenMemberWithAccessibility_WhenIsAccessibilityBelowMinimumCalled_ThenReturnsExpectedResult(
        string memberDeclaration, Accessibility minAccessibility, bool expectedResult)
    {
        // Arrange
        var code = $"public class TestClass {{ {memberDeclaration} }}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var model = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();
        var memberNode = classDecl.Members.First();
        var symbol = model.GetDeclaredSymbol(memberNode)!;

        // Act
        var result = AccessibilityFilter.IsAccessibilityBelowMinimum(symbol, minAccessibility);

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public void GivenExplicitInterfaceImplementation_WhenIsAccessibilityBelowMinimumCalled_ThenReturnsFalse()
    {
        // Arrange
        var code = @"
            public interface IFoo { void Bar(); }
            public class TestClass : IFoo { void IFoo.Bar() {} }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var model = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();
        var methodNode = classDecl.Members.First();
        var symbol = model.GetDeclaredSymbol(methodNode)!;

        // Act — explicit interface impl has Private accessibility, but should NOT be filtered
        var result = AccessibilityFilter.IsAccessibilityBelowMinimum(symbol, Accessibility.Public);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("public void Foo() {}", false)]
    [InlineData("public int Prop { get; set; }", false)]
    public void GivenNonExplicitInterfaceImpl_WhenIsExplicitInterfaceImplementationCalled_ThenReturnsFalse(
        string memberDeclaration, bool expected)
    {
        // Arrange
        var code = $"public class TestClass {{ {memberDeclaration} }}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var model = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();
        var memberNode = classDecl.Members.First();
        var symbol = model.GetDeclaredSymbol(memberNode)!;

        // Act
        var result = AccessibilityFilter.IsExplicitInterfaceImplementation(symbol);

        // Assert
        result.ShouldBe(expected);
    }
}
