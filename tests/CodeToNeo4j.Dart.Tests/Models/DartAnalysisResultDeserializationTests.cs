using System.Text.Json;
using CodeToNeo4j.Dart.Models;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Dart.Tests.Models;

public class DartAnalysisResultDeserializationTests
{
	[Fact]
	public void GivenValidJson_WhenDeserialized_ThenProducesCorrectResult()
	{
		// Arrange
		const string json = """
		                    {
		                      "projectName": "my_app",
		                      "projectRoot": "/home/user/my_app",
		                      "files": {
		                        "lib/src/foo.dart": {
		                          "symbols": [
		                            {
		                              "name": "MyClass",
		                              "kind": "DartClass",
		                              "class": "class",
		                              "fqn": "package:my_app/src/foo.dart::MyClass",
		                              "accessibility": "Public",
		                              "startLine": 10,
		                              "endLine": 50,
		                              "documentation": "/// A class",
		                              "comments": null,
		                              "namespace": "package:my_app/src",
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
		                              "toFile": "lib/src/base.dart",
		                              "relType": "src__DEPENDS_ON"
		                            }
		                          ]
		                        }
		                      }
		                    }
		                    """;

		// Act
		var result = JsonSerializer.Deserialize<DartAnalysisResult>(json);

		// Assert
		result.ShouldNotBeNull();
		result.ProjectName.ShouldBe("my_app");
		result.ProjectRoot.ShouldBe("/home/user/my_app");
		result.Files.ShouldContainKey("lib/src/foo.dart");

		var file = result.Files["lib/src/foo.dart"];
		file.Symbols.Count.ShouldBe(1);
		file.Symbols[0].Name.ShouldBe("MyClass");
		file.Symbols[0].Kind.ShouldBe("DartClass");
		file.Symbols[0].Class.ShouldBe("class");
		file.Symbols[0].Accessibility.ShouldBe("Public");
		file.Symbols[0].StartLine.ShouldBe(10);
		file.Symbols[0].EndLine.ShouldBe(50);
		file.Symbols[0].Documentation.ShouldBe("/// A class");

		file.Relationships.Count.ShouldBe(1);
		file.Relationships[0].FromSymbol.ShouldBe("MyClass");
		file.Relationships[0].ToSymbol.ShouldBe("BaseClass");
		file.Relationships[0].RelType.ShouldBe("src__DEPENDS_ON");
	}

	[Fact]
	public void GivenEmptyFilesJson_WhenDeserialized_ThenProducesEmptyFiles()
	{
		// Arrange
		const string json = """
		                    {
		                      "projectName": "empty_project",
		                      "projectRoot": "/tmp",
		                      "files": {}
		                    }
		                    """;

		// Act
		var result = JsonSerializer.Deserialize<DartAnalysisResult>(json);

		// Assert
		result.ShouldNotBeNull();
		result.ProjectName.ShouldBe("empty_project");
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
		                 "lib/main.dart": {
		                   "symbols": [
		                     {
		                       "name": "Foo",
		                       "kind": "DartClass",
		                       "class": "class",
		                       "fqn": "package:test/main.dart::Foo",
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
		var result = JsonSerializer.Deserialize<DartAnalysisResult>(json);

		// Assert
		result!.Files["lib/main.dart"].Symbols[0].Accessibility.ShouldBe(accessibility);
	}
}
