using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
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
		SymbolMapper sut = new();
		var code = "namespace MyNamespace; public class MyClass { }";
		var syntaxTree = CSharpSyntaxTree.ParseText(code);
		var compilation = CSharpCompilation.Create("Test")
			.AddSyntaxTrees(syntaxTree);
		var semanticModel = compilation.GetSemanticModel(syntaxTree);
		var node = syntaxTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();
		var symbol = semanticModel.GetDeclaredSymbol(node);

		// Act
		var result = sut.ToSymbolRecord("repo", "file", "path.cs", "MyNamespace", symbol!, node);

		// Assert
		result.Name.ShouldBe("MyClass");
		result.Kind.ShouldBe("NamedType");
		result.Fqn.ShouldContain("MyNamespace.MyClass");
		result.Class.ShouldBe("MyClass");
		result.Key.ShouldBe("repo:MyNamespace.MyClass");
		result.Accessibility.ShouldBe("Public");
		result.RelativePath.ShouldBe("path.cs");
		result.Namespace.ShouldBe("MyNamespace");
	}

	[Fact]
	public void GivenSymbolWithComments_WhenToSymbolRecordCalled_ThenCorrectCommentsReturned()
	{
		// Arrange
		SymbolMapper sut = new();
		var code = @"
namespace MyNamespace;
// This is a comment
public class MyClass { }";
		var syntaxTree = CSharpSyntaxTree.ParseText(code);
		var compilation = CSharpCompilation.Create("Test")
			.AddSyntaxTrees(syntaxTree);
		var semanticModel = compilation.GetSemanticModel(syntaxTree);
		var node = syntaxTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();
		var symbol = semanticModel.GetDeclaredSymbol(node);

		// Act
		var result = sut.ToSymbolRecord("repo", "file", "path.cs", "MyNamespace", symbol!, node);

		// Assert
		result.Comments.ShouldBe("// This is a comment");
	}

	[Fact]
	public void GivenSymbolWithMultiLineComments_WhenToSymbolRecordCalled_ThenCorrectCommentsReturned()
	{
		// Arrange
		SymbolMapper sut = new();
		var code = @"
namespace MyNamespace;
/* Multi-line
   comment */
public class MyClass { }";
		var syntaxTree = CSharpSyntaxTree.ParseText(code);
		var compilation = CSharpCompilation.Create("Test")
			.AddSyntaxTrees(syntaxTree);
		var semanticModel = compilation.GetSemanticModel(syntaxTree);
		var node = syntaxTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();
		var symbol = semanticModel.GetDeclaredSymbol(node);

		// Act
		var result = sut.ToSymbolRecord("repo", "file", "path.cs", "MyNamespace", symbol!, node);

		// Assert
		result.Comments!.ShouldContain("/* Multi-line");
		result.Comments!.ShouldContain("comment */");
	}

	[Fact]
	public void GivenSymbolWithDocumentation_WhenToSymbolRecordCalled_ThenCorrectDocumentationReturned()
	{
		// Arrange
		SymbolMapper sut = new();
		var code = @"
namespace MyNamespace;
/// <summary>My docs</summary>
public class MyClass { }";
		var syntaxTree = CSharpSyntaxTree.ParseText(code, new(documentationMode: DocumentationMode.Parse));
		var compilation = CSharpCompilation.Create("Test")
			.AddSyntaxTrees(syntaxTree)
			.WithOptions(new(OutputKind.DynamicallyLinkedLibrary));
		var semanticModel = compilation.GetSemanticModel(syntaxTree);
		var node = syntaxTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();
		var symbol = semanticModel.GetDeclaredSymbol(node);

		// Act
		var result = sut.ToSymbolRecord("repo", "file", "path.cs", "MyNamespace", symbol!, node);

		// Assert
		result.Documentation!.ShouldContain("<summary>My docs</summary>");
	}

	[Fact]
	public void GivenTechnology_WhenToSymbolRecordCalled_ThenTechnologyIsSet()
	{
		// Arrange
		SymbolMapper sut = new();
		var code = "namespace MyNamespace; public class MyClass { }";
		var syntaxTree = CSharpSyntaxTree.ParseText(code);
		var compilation = CSharpCompilation.Create("Test")
			.AddSyntaxTrees(syntaxTree);
		var semanticModel = compilation.GetSemanticModel(syntaxTree);
		var node = syntaxTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();
		var symbol = semanticModel.GetDeclaredSymbol(node);

		// Act
		var result = sut.ToSymbolRecord("repo", "file", "path.cs", "MyNamespace", symbol!, node, "csharp", "dotnet");

		// Assert
		result.Technology.ShouldBe("dotnet");
		result.Language.ShouldBe("csharp");
	}

	[Fact]
	public void GivenMethodSymbol_WhenToSymbolRecordCalled_ThenCorrectNamespaceReturned()
	{
		// Arrange
		SymbolMapper sut = new();
		var code = "namespace MyNamespace; public class MyClass { public void MyMethod() { } }";
		var syntaxTree = CSharpSyntaxTree.ParseText(code);
		var compilation = CSharpCompilation.Create("Test")
			.AddSyntaxTrees(syntaxTree);
		var semanticModel = compilation.GetSemanticModel(syntaxTree);
		var node = syntaxTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().First();
		var symbol = semanticModel.GetDeclaredSymbol(node);

		// Act
		var result = sut.ToSymbolRecord("repo", "file", "path.cs", "MyNamespace", symbol!, node);

		// Assert
		result.Namespace.ShouldBe("MyNamespace");
		result.Name.ShouldBe("MyMethod");
	}
}
