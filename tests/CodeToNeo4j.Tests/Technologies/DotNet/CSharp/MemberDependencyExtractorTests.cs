using CodeToNeo4j.Graph;
using CodeToNeo4j.Graph.Mapping;
using CodeToNeo4j.Graph.Models;
using CodeToNeo4j.Technologies.DotNet.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Technologies.DotNet.CSharp;

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
		CSharpCompilation compilation = CSharpCompilation.Create("Test",
			[tree],
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
		var model = compilation.GetSemanticModel(tree);
		var root = tree.GetRoot();

		var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
			.First(c => c.Identifier.Text == "TestClass");
		var methodNode = classDecl.Members.OfType<MethodDeclarationSyntax>().First();
		var methodSymbol = model.GetDeclaredSymbol(methodNode)!;

		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor sut = new(symbolMapper);
		List<Relationship> relBuffer = [];

		Symbol typeRec = new("test:TestClass", "TestClass", "Class", "TestClass", "TestClass", "Public", "file.cs", "file.cs", 1, 5, null, null,
			null);
		Symbol memberRec = new("test:TestClass.Foo", "Foo", "Method", "TestClass", "TestClass.Foo(Dep)", "Public", "file.cs", "file.cs", 3, 3,
			null, null, null);

		// Act
		sut.ExtractMemberDependencies(methodSymbol, methodNode, model, "test", relBuffer, typeRec, memberRec);

		// Assert
		relBuffer.ShouldContain(r => r.RelType == GraphSchema.Relationships.DependsOn && r.FromKey == typeRec.Key);
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
		CSharpCompilation compilation = CSharpCompilation.Create("Test",
			[tree],
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
		var model = compilation.GetSemanticModel(tree);
		var root = tree.GetRoot();

		var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
			.First(c => c.Identifier.Text == "TestClass");
		var methodNode = classDecl.Members.OfType<MethodDeclarationSyntax>().First();
		var methodSymbol = model.GetDeclaredSymbol(methodNode)!;

		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor sut = new(symbolMapper);
		List<Relationship> relBuffer = [];

		Symbol typeRec = new("test:TestClass", "TestClass", "Class", "TestClass", "TestClass", "Public", "file.cs", "file.cs", 1, 5, null, null,
			null);
		Symbol memberRec = new("test:TestClass.Foo", "Foo", "Method", "TestClass", "TestClass.Foo()", "Public", "file.cs", "file.cs", 3, 3, null,
			null, null);

		// Act
		sut.ExtractMemberDependencies(methodSymbol, methodNode, model, "test", relBuffer, typeRec, memberRec);

		// Assert
		relBuffer.ShouldContain(r => r.RelType == GraphSchema.Relationships.Invokes && r.FromKey == memberRec.Key);
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
		CSharpCompilation compilation = CSharpCompilation.Create("Test",
			[tree],
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
		var model = compilation.GetSemanticModel(tree);
		var root = tree.GetRoot();

		var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
			.First(c => c.Identifier.Text == "TestClass");
		var propNode = classDecl.Members.OfType<PropertyDeclarationSyntax>().First();
		var propSymbol = model.GetDeclaredSymbol(propNode)!;

		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor sut = new(symbolMapper);
		List<Relationship> relBuffer = [];

		Symbol typeRec = new("test:TestClass", "TestClass", "Class", "TestClass", "TestClass", "Public", "file.cs", "file.cs", 1, 5, null, null,
			null);
		Symbol memberRec = new("test:TestClass.MyProp", "MyProp", "Property", "TestClass", "TestClass.MyProp", "Public", "file.cs", "file.cs", 3,
			3, null, null, null);

		// Act
		sut.ExtractMemberDependencies(propSymbol, propNode, model, "test", relBuffer, typeRec, memberRec);

		// Assert
		relBuffer.ShouldContain(r => r.RelType == GraphSchema.Relationships.DependsOn && r.FromKey == typeRec.Key);
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
		CSharpCompilation compilation = CSharpCompilation.Create("Test",
			[tree],
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
		var model = compilation.GetSemanticModel(tree);
		var root = tree.GetRoot();

		var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
			.First(c => c.Identifier.Text == "TestClass");
		var fieldNode = classDecl.Members.OfType<FieldDeclarationSyntax>().First();
		var variableNode = fieldNode.Declaration.Variables.First();
		var fieldSymbol = model.GetDeclaredSymbol(variableNode)!;

		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor sut = new(symbolMapper);
		List<Relationship> relBuffer = [];

		Symbol typeRec = new("test:TestClass", "TestClass", "Class", "TestClass", "TestClass", "Public", "file.cs", "file.cs", 1, 5, null, null,
			null);
		Symbol memberRec = new("test:TestClass._field", "_field", "Field", "TestClass", "TestClass._field", "Public", "file.cs", "file.cs", 3, 3,
			null, null, null);

		// Act
		sut.ExtractMemberDependencies(fieldSymbol, variableNode, model, "test", relBuffer, typeRec, memberRec);

		// Assert
		relBuffer.ShouldContain(r => r.RelType == GraphSchema.Relationships.DependsOn && r.FromKey == typeRec.Key);
	}

	[Theory]
	[InlineData(
		"method argument",
		@"
            using System;
            public class TestClass {
                public void Subscribe(Action handler) {}
                public void HandleValue() {}
                public void Foo() { Subscribe(HandleValue); }
            }",
		"HandleValue")]
	[InlineData(
		"this-qualified method argument",
		@"
            using System;
            public class TestClass {
                public void Subscribe(Action handler) {}
                public void HandleValue() {}
                public void Foo() { Subscribe(this.HandleValue); }
            }",
		"HandleValue")]
	[InlineData(
		"constructor argument",
		@"
            using System;
            public class Command { public Command(Action execute) {} }
            public class TestClass {
                public void Execute() {}
                public void Foo() { new Command(Execute); }
            }",
		"Execute")]
	[InlineData(
		"delegate assignment",
		@"
            using System;
            public class TestClass {
                public void MyMethod() {}
                public void Foo() { Action a = MyMethod; }
            }",
		"MyMethod")]
	[InlineData(
		"event subscription",
		@"
            using System;
            public class TestClass {
                public event Action MyEvent;
                public void Handler() {}
                public void Foo() { MyEvent += Handler; }
            }",
		"Handler")]
	public void GivenMethodGroup_WhenExtractMemberDependenciesCalled_ThenAddsInvokesRelationship(
		string scenario,
		string code,
		string expectedMethodName)
	{
		// Arrange
		var tree = CSharpSyntaxTree.ParseText(code);
		CSharpCompilation compilation = CSharpCompilation.Create("Test",
			[tree],
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
		var model = compilation.GetSemanticModel(tree);
		var root = tree.GetRoot();

		var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
			.First(c => c.Identifier.Text == "TestClass");
		var methodNode = classDecl.Members.OfType<MethodDeclarationSyntax>()
			.First(m => m.Identifier.Text == "Foo");
		var methodSymbol = model.GetDeclaredSymbol(methodNode)!;

		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor sut = new(symbolMapper);
		List<Relationship> relBuffer = [];

		Symbol typeRec = new("test:TestClass", "TestClass", "Class", "TestClass", "TestClass", "Public", "file.cs", "file.cs", 1, 5, null, null,
			null);
		Symbol memberRec = new("test:TestClass.Foo", "Foo", "Method", "TestClass", "TestClass.Foo()", "Public", "file.cs", "file.cs", 3, 3, null,
			null, null);

		// Act
		sut.ExtractMemberDependencies(methodSymbol, methodNode, model, "test", relBuffer, typeRec, memberRec);

		// Assert – should have an INVOKES for the method group target
		relBuffer.ShouldContain(
			r => r.RelType == GraphSchema.Relationships.Invokes && r.FromKey == memberRec.Key && r.ToKey.Contains(expectedMethodName),
			$"Scenario '{scenario}' should create INVOKES for {expectedMethodName}");
	}

	[Fact]
	public void GivenMethodGroupAndDirectCall_WhenExtractMemberDependenciesCalled_ThenNoDuplicateInvokes()
	{
		// Arrange — Foo calls HandleValue() directly AND passes it as a method group
		var code = @"
            using System;
            public class TestClass {
                public void Subscribe(Action handler) {}
                public void HandleValue() {}
                public void Foo() {
                    HandleValue();
                    Subscribe(HandleValue);
                }
            }";
		var tree = CSharpSyntaxTree.ParseText(code);
		CSharpCompilation compilation = CSharpCompilation.Create("Test",
			[tree],
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
		var model = compilation.GetSemanticModel(tree);
		var root = tree.GetRoot();

		var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
			.First(c => c.Identifier.Text == "TestClass");
		var methodNode = classDecl.Members.OfType<MethodDeclarationSyntax>()
			.First(m => m.Identifier.Text == "Foo");
		var methodSymbol = model.GetDeclaredSymbol(methodNode)!;

		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor sut = new(symbolMapper);
		List<Relationship> relBuffer = [];

		Symbol typeRec = new("test:TestClass", "TestClass", "Class", "TestClass", "TestClass", "Public", "file.cs", "file.cs", 1, 5, null, null,
			null);
		Symbol memberRec = new("test:TestClass.Foo", "Foo", "Method", "TestClass", "TestClass.Foo()", "Public", "file.cs", "file.cs", 3, 3, null,
			null, null);

		// Act
		sut.ExtractMemberDependencies(methodSymbol, methodNode, model, "test", relBuffer, typeRec, memberRec);

		// Assert — HandleValue should appear exactly once as an INVOKES target
		List<Relationship> handleValueInvokes = relBuffer.Where(r =>
			r.RelType == GraphSchema.Relationships.Invokes && r.FromKey == memberRec.Key && r.ToKey.Contains("HandleValue")).ToList();
		handleValueInvokes.Count.ShouldBe(1, "HandleValue should have exactly one INVOKES relationship (no duplicates)");
	}

	[Fact]
	public void GivenDirectCallOnly_WhenExtractMemberDependenciesCalled_ThenNoDoubleCountFromMethodGroupDetection()
	{
		// Arrange — only a direct call, no method group; ensure the invoked method name
		// in `Helper.DoWork()` is not double-counted by method group detection
		var code = @"
            public class Helper { public static void DoWork() {} }
            public class TestClass {
                public void Foo() { Helper.DoWork(); }
            }";
		var tree = CSharpSyntaxTree.ParseText(code);
		CSharpCompilation compilation = CSharpCompilation.Create("Test",
			[tree],
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
		var model = compilation.GetSemanticModel(tree);
		var root = tree.GetRoot();

		var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
			.First(c => c.Identifier.Text == "TestClass");
		var methodNode = classDecl.Members.OfType<MethodDeclarationSyntax>().First();
		var methodSymbol = model.GetDeclaredSymbol(methodNode)!;

		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor sut = new(symbolMapper);
		List<Relationship> relBuffer = [];

		Symbol typeRec = new("test:TestClass", "TestClass", "Class", "TestClass", "TestClass", "Public", "file.cs", "file.cs", 1, 5, null, null,
			null);
		Symbol memberRec = new("test:TestClass.Foo", "Foo", "Method", "TestClass", "TestClass.Foo()", "Public", "file.cs", "file.cs", 3, 3, null,
			null, null);

		// Act
		sut.ExtractMemberDependencies(methodSymbol, methodNode, model, "test", relBuffer, typeRec, memberRec);

		// Assert — DoWork should appear exactly once
		List<Relationship> doWorkInvokes = relBuffer.Where(r =>
			r.RelType == GraphSchema.Relationships.Invokes && r.ToKey.Contains("DoWork")).ToList();
		doWorkInvokes.Count.ShouldBe(1, "Direct call should produce exactly one INVOKES, not double-counted by method group detection");
	}

	[Fact]
	public void GivenLambdaWithMethodCall_WhenExtractMemberDependenciesCalled_ThenAddsInvokesForCallInsideLambda()
	{
		// Arrange — verify existing behavior: a method called inside a lambda is detected
		var code = @"
            using System;
            public class TestClass {
                public void Subscribe(Action handler) {}
                public void HandleValue() {}
                public void Foo() { Subscribe(() => HandleValue()); }
            }";
		var tree = CSharpSyntaxTree.ParseText(code);
		CSharpCompilation compilation = CSharpCompilation.Create("Test",
			[tree],
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
		var model = compilation.GetSemanticModel(tree);
		var root = tree.GetRoot();

		var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
			.First(c => c.Identifier.Text == "TestClass");
		var methodNode = classDecl.Members.OfType<MethodDeclarationSyntax>()
			.First(m => m.Identifier.Text == "Foo");
		var methodSymbol = model.GetDeclaredSymbol(methodNode)!;

		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor sut = new(symbolMapper);
		List<Relationship> relBuffer = [];

		Symbol typeRec = new("test:TestClass", "TestClass", "Class", "TestClass", "TestClass", "Public", "file.cs", "file.cs", 1, 5, null, null,
			null);
		Symbol memberRec = new("test:TestClass.Foo", "Foo", "Method", "TestClass", "TestClass.Foo()", "Public", "file.cs", "file.cs", 3, 3, null,
			null, null);

		// Act
		sut.ExtractMemberDependencies(methodSymbol, methodNode, model, "test", relBuffer, typeRec, memberRec);

		// Assert
		relBuffer.ShouldContain(
			r => r.RelType == GraphSchema.Relationships.Invokes && r.FromKey == memberRec.Key && r.ToKey.Contains("HandleValue"),
			"Lambda body invocation of HandleValue should be detected");
	}

	[Fact]
	public void GivenExternalNamespaceSymbol_WhenAddDependsOnIfExternalCalled_ThenAddsRelationship()
	{
		// Arrange
		var code = "using System; public class TestClass {}";
		var tree = CSharpSyntaxTree.ParseText(code);
		CSharpCompilation compilation = CSharpCompilation.Create("Test",
			[tree],
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
		var model = compilation.GetSemanticModel(tree);
		var root = tree.GetRoot();
		var usingDirective = root.DescendantNodes().OfType<UsingDirectiveSyntax>().First();
		var usingSymbol = model.GetSymbolInfo(usingDirective.Name!).Symbol;

		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor sut = new(symbolMapper);
		List<Relationship> relBuffer = [];

		// Act
		sut.AddDependsOnIfExternal(usingSymbol, compilation.Assembly, "test", "file.cs", relBuffer);

		// Assert
		relBuffer.ShouldContain(r => r.RelType == GraphSchema.Relationships.DependsOn && r.FromKey == "file.cs");
	}

	[Fact]
	public void GivenNullSymbol_WhenAddDependsOnIfExternalCalled_ThenNoRelationshipAdded()
	{
		// Arrange
		var code = "public class TestClass {}";
		var tree = CSharpSyntaxTree.ParseText(code);
		CSharpCompilation compilation = CSharpCompilation.Create("Test",
			[tree],
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor sut = new(symbolMapper);
		List<Relationship> relBuffer = [];

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
			r.RelType == GraphSchema.Relationships.Invokes && r.ToKey.Contains(expectedFragment));
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
			r.RelType == GraphSchema.Relationships.Invokes && r.ToKey.Contains(expectedFragment));
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
			r.RelType == GraphSchema.Relationships.Invokes && r.ToKey.Contains(expectedFragment));
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
			r.RelType == GraphSchema.Relationships.Invokes && r.ToKey.Contains(expectedFragment));
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
			r.RelType == GraphSchema.Relationships.Invokes && r.ToKey.Contains(expectedFragment));
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
			r.RelType == GraphSchema.Relationships.Invokes && r.ToKey.Contains(expectedFragment));
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
			r.RelType == GraphSchema.Relationships.Invokes && r.ToKey.Contains(expectedFragment));
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
			r.RelType == GraphSchema.Relationships.Invokes && r.ToKey.Contains("explicit operator int(Foo)"));
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
			r.RelType == GraphSchema.Relationships.Invokes && r.ToKey.Contains("implicit operator string(Foo)"));
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
			r.RelType == GraphSchema.Relationships.Invokes && r.ToKey.Contains("implicit operator Foo(FooFixture)"));
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

		relBuffer.ShouldNotContain(r => r.RelType == GraphSchema.Relationships.Invokes && r.ToKey.Contains("operator"));
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
			r.RelType == GraphSchema.Relationships.Invokes && r.ToKey.Contains("operator ==(Foo, Foo)")).ShouldBe(1);
	}

	private static List<Relationship> ExtractRelationships(string code, string className, string methodName)
	{
		var tree = CSharpSyntaxTree.ParseText(code);
		CSharpCompilation compilation = CSharpCompilation.Create("Test",
			[tree],
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
		var model = compilation.GetSemanticModel(tree);
		var root = tree.GetRoot();

		var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
			.First(c => c.Identifier.Text == className);
		var methodNode = classDecl.Members.OfType<MethodDeclarationSyntax>()
			.First(m => m.Identifier.Text == methodName);
		var methodSymbol = model.GetDeclaredSymbol(methodNode)!;

		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor sut = new(symbolMapper);
		List<Relationship> relBuffer = [];

		Symbol typeRec = new($"test:{className}", className, "Class", className, className, "Public", "file.cs", "file.cs", 1, 10, null, null,
			null);
		Symbol memberRec = new($"test:{className}.{methodName}", methodName, "Method", className, $"{className}.{methodName}()", "Public",
			"file.cs", "file.cs", 3, 3, null, null, null);

		sut.ExtractMemberDependencies(methodSymbol, methodNode, model, "test", relBuffer, typeRec, memberRec);

		return relBuffer;
	}
}
