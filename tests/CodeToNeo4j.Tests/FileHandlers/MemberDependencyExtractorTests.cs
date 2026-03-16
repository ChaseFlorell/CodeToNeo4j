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

    [Theory]
    [InlineData("==", "operator ==(Foo, Foo)")]
    [InlineData("!=", "operator !=(Foo, Foo)")]
    public void GivenEqualityOperatorUsage_WhenExtracted_ThenAddsInvokesRelationship(string op, string expectedFragment)
    {
        var code = $$"""
            public class Foo {
                public int Value;
                public static bool operator ==(Foo a, Foo b) => a.Value == b.Value;
                public static bool operator !=(Foo a, Foo b) => !(a == b);
                public override bool Equals(object obj) => obj is Foo f && this == f;
                public override int GetHashCode() => Value;
            }
            public class TestClass {
                public void Use(Foo a, Foo b) { var r = a {{op}} b; }
            }
            """;

        var relBuffer = ExtractRelationships(code, "TestClass", "Use");

        relBuffer.ShouldContain(r =>
            r.RelType == "INVOKES" && r.ToKey.Contains(expectedFragment));
    }

    [Theory]
    [InlineData(">", "operator >(Foo, Foo)")]
    [InlineData("<", "operator <(Foo, Foo)")]
    [InlineData(">=", "operator >=(Foo, Foo)")]
    [InlineData("<=", "operator <=(Foo, Foo)")]
    public void GivenComparisonOperatorUsage_WhenExtracted_ThenAddsInvokesRelationship(string op, string expectedFragment)
    {
        var code = $$"""
            public class Foo {
                public int Value;
                public static bool operator >(Foo a, Foo b) => a.Value > b.Value;
                public static bool operator <(Foo a, Foo b) => a.Value < b.Value;
                public static bool operator >=(Foo a, Foo b) => a.Value >= b.Value;
                public static bool operator <=(Foo a, Foo b) => a.Value <= b.Value;
            }
            public class TestClass {
                public void Use(Foo a, Foo b) { var r = a {{op}} b; }
            }
            """;

        var relBuffer = ExtractRelationships(code, "TestClass", "Use");

        relBuffer.ShouldContain(r =>
            r.RelType == "INVOKES" && r.ToKey.Contains(expectedFragment));
    }

    [Theory]
    [InlineData("|", "operator |(Foo, Foo)")]
    [InlineData("&", "operator &(Foo, Foo)")]
    [InlineData("^", "operator ^(Foo, Foo)")]
    public void GivenBitwiseOperatorUsage_WhenExtracted_ThenAddsInvokesRelationship(string op, string expectedFragment)
    {
        var code = $$"""
            public class Foo {
                public int Value;
                public static Foo operator |(Foo a, Foo b) => new Foo { Value = a.Value | b.Value };
                public static Foo operator &(Foo a, Foo b) => new Foo { Value = a.Value & b.Value };
                public static Foo operator ^(Foo a, Foo b) => new Foo { Value = a.Value ^ b.Value };
            }
            public class TestClass {
                public void Use(Foo a, Foo b) { var r = a {{op}} b; }
            }
            """;

        var relBuffer = ExtractRelationships(code, "TestClass", "Use");

        relBuffer.ShouldContain(r =>
            r.RelType == "INVOKES" && r.ToKey.Contains(expectedFragment));
    }

    [Theory]
    [InlineData("+", "operator +(Foo, Foo)")]
    [InlineData("-", "operator -(Foo, Foo)")]
    [InlineData("*", "operator *(Foo, Foo)")]
    [InlineData("/", "operator /(Foo, Foo)")]
    [InlineData("%", "operator %(Foo, Foo)")]
    public void GivenArithmeticOperatorUsage_WhenExtracted_ThenAddsInvokesRelationship(string op, string expectedFragment)
    {
        var code = $$"""
            public class Foo {
                public int Value;
                public static Foo operator +(Foo a, Foo b) => new Foo { Value = a.Value + b.Value };
                public static Foo operator -(Foo a, Foo b) => new Foo { Value = a.Value - b.Value };
                public static Foo operator *(Foo a, Foo b) => new Foo { Value = a.Value * b.Value };
                public static Foo operator /(Foo a, Foo b) => new Foo { Value = a.Value / b.Value };
                public static Foo operator %(Foo a, Foo b) => new Foo { Value = a.Value % b.Value };
            }
            public class TestClass {
                public void Use(Foo a, Foo b) { var r = a {{op}} b; }
            }
            """;

        var relBuffer = ExtractRelationships(code, "TestClass", "Use");

        relBuffer.ShouldContain(r =>
            r.RelType == "INVOKES" && r.ToKey.Contains(expectedFragment));
    }

    [Theory]
    [InlineData("<<", "operator <<(Foo, int)")]
    [InlineData(">>", "operator >>(Foo, int)")]
    public void GivenShiftOperatorUsage_WhenExtracted_ThenAddsInvokesRelationship(string op, string expectedFragment)
    {
        var code = $$"""
            public class Foo {
                public int Value;
                public static Foo operator <<(Foo a, int b) => new Foo { Value = a.Value << b };
                public static Foo operator >>(Foo a, int b) => new Foo { Value = a.Value >> b };
            }
            public class TestClass {
                public void Use(Foo a) { var r = a {{op}} 2; }
            }
            """;

        var relBuffer = ExtractRelationships(code, "TestClass", "Use");

        relBuffer.ShouldContain(r =>
            r.RelType == "INVOKES" && r.ToKey.Contains(expectedFragment));
    }

    [Theory]
    [InlineData("!", "operator !(Foo)")]
    [InlineData("~", "operator ~(Foo)")]
    public void GivenUnaryOperatorUsage_WhenExtracted_ThenAddsInvokesRelationship(string op, string expectedFragment)
    {
        var code = $$"""
            public class Foo {
                public int Value;
                public static Foo operator !(Foo a) => new Foo { Value = a.Value == 0 ? 1 : 0 };
                public static Foo operator ~(Foo a) => new Foo { Value = ~a.Value };
            }
            public class TestClass {
                public void Use(Foo a) { var r = {{op}}a; }
            }
            """;

        var relBuffer = ExtractRelationships(code, "TestClass", "Use");

        relBuffer.ShouldContain(r =>
            r.RelType == "INVOKES" && r.ToKey.Contains(expectedFragment));
    }

    [Theory]
    [InlineData("++", "operator ++(Foo)")]
    [InlineData("--", "operator --(Foo)")]
    public void GivenIncrementDecrementOperatorUsage_WhenExtracted_ThenAddsInvokesRelationship(string op, string expectedFragment)
    {
        var code = $$"""
            public class Foo {
                public int Value;
                public static Foo operator ++(Foo a) => new Foo { Value = a.Value + 1 };
                public static Foo operator --(Foo a) => new Foo { Value = a.Value - 1 };
            }
            public class TestClass {
                public void Use(Foo a) { {{op}}a; }
            }
            """;

        var relBuffer = ExtractRelationships(code, "TestClass", "Use");

        relBuffer.ShouldContain(r =>
            r.RelType == "INVOKES" && r.ToKey.Contains(expectedFragment));
    }

    [Fact]
    public void GivenExplicitCastOperatorUsage_WhenExtracted_ThenAddsInvokesRelationship()
    {
        var code = """
            public class Foo {
                public int Value;
                public static explicit operator int(Foo a) => a.Value;
            }
            public class TestClass {
                public void Use(Foo f) { var x = (int)f; }
            }
            """;

        var relBuffer = ExtractRelationships(code, "TestClass", "Use");

        relBuffer.ShouldContain(r =>
            r.RelType == "INVOKES" && r.ToKey.Contains("explicit operator int(Foo)"));
    }

    [Fact]
    public void GivenImplicitConversionViaAssignment_WhenExtracted_ThenAddsInvokesRelationship()
    {
        var code = """
            public class Foo {
                public static implicit operator string(Foo a) => "foo";
            }
            public class TestClass {
                public void Use() {
                    Foo f = new Foo();
                    string s = f;
                }
            }
            """;

        var relBuffer = ExtractRelationships(code, "TestClass", "Use");

        relBuffer.ShouldContain(r =>
            r.RelType == "INVOKES" && r.ToKey.Contains("implicit operator string(Foo)"));
    }

    [Fact]
    public void GivenImplicitConversionViaVariableInitializer_WhenExtracted_ThenAddsInvokesRelationship()
    {
        var code = """
            public class FooFixture {
                public static implicit operator Foo(FooFixture a) => new Foo();
            }
            public class Foo {}
            public class TestClass {
                public void Use() {
                    Foo f = new FooFixture();
                }
            }
            """;

        var relBuffer = ExtractRelationships(code, "TestClass", "Use");

        relBuffer.ShouldContain(r =>
            r.RelType == "INVOKES" && r.ToKey.Contains("implicit operator Foo(FooFixture)"));
    }

    [Fact]
    public void GivenBuiltInOperator_WhenExtracted_ThenDoesNotAddInvokesRelationship()
    {
        var code = """
            public class TestClass {
                public void Use() { var r = 1 + 2; var b = 1 == 2; }
            }
            """;

        var relBuffer = ExtractRelationships(code, "TestClass", "Use");

        relBuffer.ShouldNotContain(r => r.RelType == "INVOKES" && r.ToKey.Contains("operator"));
    }

    [Fact]
    public void GivenDuplicateOperatorUsage_WhenExtracted_ThenAddsOnlyOneInvokesRelationship()
    {
        var code = """
            public class Foo {
                public int Value;
                public static bool operator ==(Foo a, Foo b) => a.Value == b.Value;
                public static bool operator !=(Foo a, Foo b) => !(a == b);
                public override bool Equals(object obj) => obj is Foo f && this == f;
                public override int GetHashCode() => Value;
            }
            public class TestClass {
                public void Use(Foo a, Foo b) { var r1 = a == b; var r2 = a == b; }
            }
            """;

        var relBuffer = ExtractRelationships(code, "TestClass", "Use");

        relBuffer.Count(r =>
            r.RelType == "INVOKES" && r.ToKey.Contains("operator ==(Foo, Foo)")).ShouldBe(1);
    }

    private static List<Relationship> ExtractRelationships(string code, string className, string methodName)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var model = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();

        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == className);
        var methodNode = classDecl.Members.OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == methodName);
        var methodSymbol = model.GetDeclaredSymbol(methodNode)!;

        var symbolMapper = new SymbolMapper();
        var sut = new MemberDependencyExtractor(symbolMapper);
        var relBuffer = new List<Relationship>();

        var typeRec = new Symbol($"test:{className}", className, "Class", className, className, "Public", "file.cs", "file.cs", 1, 10, null, null, null);
        var memberRec = new Symbol($"test:{className}.{methodName}", methodName, "Method", className, $"{className}.{methodName}()", "Public", "file.cs", "file.cs", 3, 3, null, null, null);

        sut.ExtractMemberDependencies(methodSymbol, methodNode, model, "test", relBuffer, typeRec, memberRec);

        return relBuffer;
    }
}
