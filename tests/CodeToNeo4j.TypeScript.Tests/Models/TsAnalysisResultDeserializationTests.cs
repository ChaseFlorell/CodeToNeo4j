using System.Text.Json;
using CodeToNeo4j.TypeScript.Models;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.TypeScript.Tests.Models;

public class TsAnalysisResultDeserializationTests
{
	[Fact]
	public void GivenValidJson_WhenDeserialized_ThenProducesCorrectResult()
	{
		// Arrange
		const string json = """
		                    {
		                      "projectName": "my-app",
		                      "projectRoot": "/home/user/my-app",
		                      "files": {
		                        "src/foo.ts": {
		                          "symbols": [
		                            {
		                              "name": "MyClass",
		                              "kind": "TypeScriptClass",
		                              "class": "class",
		                              "fqn": "@my-app/src/foo.ts::MyClass",
		                              "accessibility": "Public",
		                              "startLine": 10,
		                              "endLine": 50,
		                              "documentation": "/** A class */",
		                              "comments": null,
		                              "namespace": "@my-app/src",
		                              "containingClass": null
		                            }
		                          ],
		                          "relationships": [
		                            {
		                              "fromSymbol": "MyClass",
		                              "fromKind": "class",
		                              "fromLine": 10,
		                              "toSymbol": "BaseClass",
		                              "toKind": "class",
		                              "toLine": null,
		                              "toFile": "src/base.ts",
		                              "relType": "src__DEPENDS_ON"
		                            }
		                          ]
		                        }
		                      }
		                    }
		                    """;

		// Act
		var result = JsonSerializer.Deserialize<TsAnalysisResult>(json);

		// Assert
		result.ShouldNotBeNull();
		result.ProjectName.ShouldBe("my-app");
		result.ProjectRoot.ShouldBe("/home/user/my-app");
		result.Files.ShouldContainKey("src/foo.ts");

		var file = result.Files["src/foo.ts"];
		file.Symbols.Count.ShouldBe(1);
		file.Symbols[0].Name.ShouldBe("MyClass");
		file.Symbols[0].Kind.ShouldBe("TypeScriptClass");
		file.Symbols[0].Class.ShouldBe("class");
		file.Symbols[0].Accessibility.ShouldBe("Public");
		file.Symbols[0].StartLine.ShouldBe(10);
		file.Symbols[0].EndLine.ShouldBe(50);
		file.Symbols[0].Documentation.ShouldBe("/** A class */");
		file.Symbols[0].Comments.ShouldBeNull();
		file.Symbols[0].Namespace.ShouldBe("@my-app/src");
		file.Symbols[0].ContainingClass.ShouldBeNull();

		file.Relationships.Count.ShouldBe(1);
		file.Relationships[0].FromSymbol.ShouldBe("MyClass");
		file.Relationships[0].ToSymbol.ShouldBe("BaseClass");
		file.Relationships[0].ToLine.ShouldBeNull();
		file.Relationships[0].ToFile.ShouldBe("src/base.ts");
		file.Relationships[0].RelType.ShouldBe("src__DEPENDS_ON");
	}

	[Fact]
	public void GivenEmptyFilesJson_WhenDeserialized_ThenProducesEmptyFiles()
	{
		// Arrange
		const string json = """
		                    {
		                      "projectName": "empty-project",
		                      "projectRoot": "/tmp",
		                      "files": {}
		                    }
		                    """;

		// Act
		var result = JsonSerializer.Deserialize<TsAnalysisResult>(json);

		// Assert
		result.ShouldNotBeNull();
		result.ProjectName.ShouldBe("empty-project");
		result.Files.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("Public")]
	[InlineData("Private")]
	[InlineData("Protected")]
	[InlineData("Internal")]
	public void GivenSymbolWithAccessibility_WhenDeserialized_ThenAccessibilityIsPreserved(string accessibility)
	{
		// Arrange
		var json = $$"""
		             {
		               "projectName": "test",
		               "projectRoot": "/tmp",
		               "files": {
		                 "src/main.ts": {
		                   "symbols": [
		                     {
		                       "name": "Foo",
		                       "kind": "TypeScriptClass",
		                       "class": "class",
		                       "fqn": "@test/src/main.ts::Foo",
		                       "accessibility": "{{accessibility}}",
		                       "startLine": 1,
		                       "endLine": 5,
		                       "documentation": null,
		                       "comments": null,
		                       "namespace": null,
		                       "containingClass": null
		                     }
		                   ],
		                   "relationships": []
		                 }
		               }
		             }
		             """;

		// Act
		var result = JsonSerializer.Deserialize<TsAnalysisResult>(json);

		// Assert
		result!.Files["src/main.ts"].Symbols[0].Accessibility.ShouldBe(accessibility);
	}

	[Fact]
	public void GivenMultipleFilesAndSymbols_WhenDeserialized_ThenAllPopulated()
	{
		// Arrange
		const string json = """
		                    {
		                      "projectName": "multi",
		                      "projectRoot": "/tmp/multi",
		                      "files": {
		                        "src/a.ts": {
		                          "symbols": [
		                            {
		                              "name": "A",
		                              "kind": "TypeScriptClass",
		                              "class": "class",
		                              "fqn": "@multi/src/a.ts::A",
		                              "accessibility": "Public",
		                              "startLine": 1,
		                              "endLine": 10,
		                              "documentation": null,
		                              "comments": null,
		                              "namespace": "@multi/src",
		                              "containingClass": null
		                            }
		                          ],
		                          "relationships": []
		                        },
		                        "src/b.ts": {
		                          "symbols": [
		                            {
		                              "name": "doWork",
		                              "kind": "TypeScriptMethod",
		                              "class": "method",
		                              "fqn": "@multi/src/b.ts::B.doWork",
		                              "accessibility": "Private",
		                              "startLine": 5,
		                              "endLine": 8,
		                              "documentation": null,
		                              "comments": "// internal helper",
		                              "namespace": "@multi/src",
		                              "containingClass": "B"
		                            }
		                          ],
		                          "relationships": []
		                        }
		                      }
		                    }
		                    """;

		// Act
		var result = JsonSerializer.Deserialize<TsAnalysisResult>(json);

		// Assert
		result.ShouldNotBeNull();
		result.Files.Count.ShouldBe(2);
		result.Files["src/a.ts"].Symbols[0].Name.ShouldBe("A");
		result.Files["src/b.ts"].Symbols[0].ContainingClass.ShouldBe("B");
		result.Files["src/b.ts"].Symbols[0].Comments.ShouldBe("// internal helper");
	}
}
