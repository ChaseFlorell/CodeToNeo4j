using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class MemberDependencyExtractorTests
{
    [Fact]
    public void GivenMethodWithParameter_WhenExtractMemberDependenciesCalled_ThenAddsDependsOnForParameterType()
    {
        // Arrange
        var code = @"
            public class Dep {}
            public class TestClass {
                public void Foo(Dep d) {}
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var model = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();

        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "TestClass");
        var methodNode = classDecl.Members.OfType<MethodDeclarationSyntax>().First();
        var methodSymbol = model.GetDeclaredSymbol(methodNode)!;

        var symbolMapper = new SymbolMapper();
        var sut = new MemberDependencyExtractor(symbolMapper);
        var relBuffer = new List<Relationship>();

        var typeRec = new Symbol("test:TestClass", "TestClass", "Class", "TestClass", "TestClass", "Public", "file.cs", "file.cs", 1, 5, null, null, null);
        var memberRec = new Symbol("test:TestClass.Foo", "Foo", "Method", "TestClass", "TestClass.Foo(Dep)", "Public", "file.cs", "file.cs", 3, 3, null, null, null);

        // Act
        sut.ExtractMemberDependencies(methodSymbol, methodNode, model, "test", relBuffer, typeRec, memberRec);

        // Assert
        relBuffer.ShouldContain(r => r.RelType == "DEPENDS_ON" && r.FromKey == typeRec.Key);
    }

    [Fact]
    public void GivenMethodWithInvocation_WhenExtractMemberDependenciesCalled_ThenAddsInvokesRelationship()
    {
        // Arrange
        var code = @"
            public class Helper { public static void DoWork() {} }
            public class TestClass {
                public void Foo() { Helper.DoWork(); }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var model = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();

        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "TestClass");
        var methodNode = classDecl.Members.OfType<MethodDeclarationSyntax>().First();
        var methodSymbol = model.GetDeclaredSymbol(methodNode)!;

        var symbolMapper = new SymbolMapper();
        var sut = new MemberDependencyExtractor(symbolMapper);
        var relBuffer = new List<Relationship>();

        var typeRec = new Symbol("test:TestClass", "TestClass", "Class", "TestClass", "TestClass", "Public", "file.cs", "file.cs", 1, 5, null, null, null);
        var memberRec = new Symbol("test:TestClass.Foo", "Foo", "Method", "TestClass", "TestClass.Foo()", "Public", "file.cs", "file.cs", 3, 3, null, null, null);

        // Act
        sut.ExtractMemberDependencies(methodSymbol, methodNode, model, "test", relBuffer, typeRec, memberRec);

        // Assert
        relBuffer.ShouldContain(r => r.RelType == "INVOKES" && r.FromKey == memberRec.Key);
    }

    [Fact]
    public void GivenPropertyMember_WhenExtractMemberDependenciesCalled_ThenAddsDependsOnForPropertyType()
    {
        // Arrange
        var code = @"
            public class Dep {}
            public class TestClass {
                public Dep MyProp { get; set; }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var model = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();

        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "TestClass");
        var propNode = classDecl.Members.OfType<PropertyDeclarationSyntax>().First();
        var propSymbol = model.GetDeclaredSymbol(propNode)!;

        var symbolMapper = new SymbolMapper();
        var sut = new MemberDependencyExtractor(symbolMapper);
        var relBuffer = new List<Relationship>();

        var typeRec = new Symbol("test:TestClass", "TestClass", "Class", "TestClass", "TestClass", "Public", "file.cs", "file.cs", 1, 5, null, null, null);
        var memberRec = new Symbol("test:TestClass.MyProp", "MyProp", "Property", "TestClass", "TestClass.MyProp", "Public", "file.cs", "file.cs", 3, 3, null, null, null);

        // Act
        sut.ExtractMemberDependencies(propSymbol, propNode, model, "test", relBuffer, typeRec, memberRec);

        // Assert
        relBuffer.ShouldContain(r => r.RelType == "DEPENDS_ON" && r.FromKey == typeRec.Key);
    }

    [Fact]
    public void GivenFieldMember_WhenExtractMemberDependenciesCalled_ThenAddsDependsOnForFieldType()
    {
        // Arrange
        var code = @"
            public class Dep {}
            public class TestClass {
                public Dep _field;
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var model = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();

        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "TestClass");
        var fieldNode = classDecl.Members.OfType<FieldDeclarationSyntax>().First();
        var variableNode = fieldNode.Declaration.Variables.First();
        var fieldSymbol = model.GetDeclaredSymbol(variableNode)!;

        var symbolMapper = new SymbolMapper();
        var sut = new MemberDependencyExtractor(symbolMapper);
        var relBuffer = new List<Relationship>();

        var typeRec = new Symbol("test:TestClass", "TestClass", "Class", "TestClass", "TestClass", "Public", "file.cs", "file.cs", 1, 5, null, null, null);
        var memberRec = new Symbol("test:TestClass._field", "_field", "Field", "TestClass", "TestClass._field", "Public", "file.cs", "file.cs", 3, 3, null, null, null);

        // Act
        sut.ExtractMemberDependencies(fieldSymbol, variableNode, model, "test", relBuffer, typeRec, memberRec);

        // Assert
        relBuffer.ShouldContain(r => r.RelType == "DEPENDS_ON" && r.FromKey == typeRec.Key);
    }

    [Fact]
    public void GivenExternalNamespaceSymbol_WhenAddDependsOnIfExternalCalled_ThenAddsRelationship()
    {
        // Arrange
        var code = "using System; public class TestClass {}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var model = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();
        var usingDirective = root.DescendantNodes().OfType<UsingDirectiveSyntax>().First();
        var usingSymbol = model.GetSymbolInfo(usingDirective.Name!).Symbol;

        var symbolMapper = new SymbolMapper();
        var sut = new MemberDependencyExtractor(symbolMapper);
        var relBuffer = new List<Relationship>();

        // Act
        sut.AddDependsOnIfExternal(usingSymbol, compilation.Assembly, "test", "file.cs", relBuffer);

        // Assert
        relBuffer.ShouldContain(r => r.RelType == "DEPENDS_ON" && r.FromKey == "file.cs");
    }

    [Fact]
    public void GivenNullSymbol_WhenAddDependsOnIfExternalCalled_ThenNoRelationshipAdded()
    {
        // Arrange
        var code = "public class TestClass {}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var symbolMapper = new SymbolMapper();
        var sut = new MemberDependencyExtractor(symbolMapper);
        var relBuffer = new List<Relationship>();

        // Act
        sut.AddDependsOnIfExternal(null, compilation.Assembly, "test", "file.cs", relBuffer);

        // Assert
        relBuffer.ShouldBeEmpty();
    }
}
