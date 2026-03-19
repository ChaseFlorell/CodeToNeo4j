using CodeToNeo4j.Dart.Yaml;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Dart.Tests.Yaml;

public class PubspecParserTests
{
	[Fact]
	public void GivenPubspecWithNameAndDeps_WhenParsed_ThenExtractsAll()
	{
		// Arrange
		const string content = """
		                       name: my_app
		                       version: 1.0.0
		                       dependencies:
		                         flutter:
		                           sdk: flutter
		                         http: ^0.13.0
		                         path: ^1.9.0
		                       dev_dependencies:
		                         flutter_test:
		                           sdk: flutter
		                         mockito: ^5.0.0
		                       """;

		// Act
		var result = PubspecParser.Parse(content);

		// Assert
		result.Name.ShouldBe("my_app");
		result.Dependencies.ShouldContain(d => d.Name == "http" && d.Version == "^0.13.0" && !d.IsDev);
		result.Dependencies.ShouldContain(d => d.Name == "path" && d.Version == "^1.9.0" && !d.IsDev);
		result.DevDependencies.ShouldContain(d => d.Name == "mockito" && d.Version == "^5.0.0" && d.IsDev);
	}

	[Fact]
	public void GivenPubspecWithOnlyName_WhenParsed_ThenDependenciesAreEmpty()
	{
		// Arrange
		const string content = """
		                       name: simple_app
		                       version: 1.0.0
		                       """;

		// Act
		var result = PubspecParser.Parse(content);

		// Assert
		result.Name.ShouldBe("simple_app");
		result.Dependencies.ShouldBeEmpty();
		result.DevDependencies.ShouldBeEmpty();
	}

	[Fact]
	public void GivenEmptyContent_WhenParsed_ThenReturnsEmptyResult()
	{
		// Act
		var result = PubspecParser.Parse(string.Empty);

		// Assert
		result.Name.ShouldBeEmpty();
		result.Dependencies.ShouldBeEmpty();
		result.DevDependencies.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("http", "^0.13.0", false)]
	[InlineData("mockito", "^5.0.0", true)]
	public void GivenDependency_WhenParsed_ThenIDevFlagIsCorrect(string depName, string expectedVersion, bool expectedIsDev)
	{
		// Arrange
		const string content = """
		                       name: test_app
		                       dependencies:
		                         http: ^0.13.0
		                       dev_dependencies:
		                         mockito: ^5.0.0
		                       """;

		// Act
		var result = PubspecParser.Parse(content);

		// Assert
		List<PubspecDependency> allDeps = result.Dependencies.Concat(result.DevDependencies).ToList();
		var dep = allDeps.First(d => d.Name == depName);
		dep.Version.ShouldBe(expectedVersion);
		dep.IsDev.ShouldBe(expectedIsDev);
	}

	[Fact]
	public void GivenPathDependency_WhenParsed_ThenVersionIsNull()
	{
		// Arrange
		const string content = """
		                       name: test_app
		                       dependencies:
		                         flutter:
		                           sdk: flutter
		                       """;

		// Act
		var result = PubspecParser.Parse(content);

		// Assert
		// "flutter:" has no version on the same line, followed by an indented "sdk: flutter"
		// The parser should pick up "flutter" with null version, and "sdk" as a sub-key is ignored
		result.Dependencies.ShouldContain(d => d.Name == "flutter" && d.Version == null);
	}
}
