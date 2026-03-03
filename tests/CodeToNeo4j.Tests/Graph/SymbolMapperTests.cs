using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Graph;

public class SymbolMapperTests
{
    [Fact]
    public void GivenTypeSymbol_WhenToSymbolRecordCalled_ThenCorrectSymbolRecordReturned()
    {
        // Arrange
        var sut = new SymbolMapper();
        var code = "namespace MyNamespace; public class MyClass { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test")
            .AddSyntaxTrees(syntaxTree);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var node = syntaxTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();
        var symbol = semanticModel.GetDeclaredSymbol(node);

        // Act
        var result = sut.ToSymbolRecord("repo", "file", "path.cs", symbol!, node);

        // Assert
        result.Name.ShouldBe("MyClass");
        result.Kind.ShouldBe("NamedType");
        result.Fqn.ShouldContain("MyNamespace.MyClass");
        result.Key.ShouldBe("repo:MyNamespace.MyClass");
        result.Accessibility.ShouldBe("Public");
    }

    [Fact]
    public void GivenTypeSymbol_WhenBuildStableSymbolKeyCalled_ThenCorrectKeyReturned()
    {
        // Arrange
        var sut = new SymbolMapper();
        var code = "namespace MyNamespace; public class MyClass { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test")
            .AddSyntaxTrees(syntaxTree);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var node = syntaxTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();
        var symbol = semanticModel.GetDeclaredSymbol(node);

        // Act
        var result = sut.BuildStableSymbolKey("repo", symbol!);

        // Assert
        result.ShouldBe("repo:MyNamespace.MyClass");
    }
}
