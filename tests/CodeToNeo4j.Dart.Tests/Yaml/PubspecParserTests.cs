using CodeToNeo4j.Dart.Yaml;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Dart.Tests.Yaml;

public class PubspecParserTests
{
	private readonly PubspecParser _sut = new();

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
		var result = _sut.Parse(content);

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
		var result = _sut.Parse(content);

		// Assert
		result.Name.ShouldBe("simple_app");
		result.Dependencies.ShouldBeEmpty();
		result.DevDependencies.ShouldBeEmpty();
	}

	[Fact]
	public void GivenEmptyContent_WhenParsed_ThenReturnsEmptyResult()
	{
		// Act
		var result = _sut.Parse(string.Empty);

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
		var result = _sut.Parse(content);

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
		var result = _sut.Parse(content);

		// Assert
		// "flutter:" has no version on the same line, followed by an indented "sdk: flutter"
		// The parser should pick up "flutter" with null version, and "sdk" as a sub-key is ignored
		result.Dependencies.ShouldContain(d => d.Name == "flutter" && d.Version == null);
	}

	[Fact]
	public void GivenPubspecWithSdkConstraint_WhenParsed_ThenSdkConstraintIsExtracted()
	{
		// Arrange
		const string content = """
		                       name: my_app
		                       environment:
		                         sdk: ">=3.0.0 <4.0.0"
		                       dependencies:
		                         http: ^0.13.0
		                       """;

		// Act
		var result = _sut.Parse(content);

		// Assert
		result.SdkConstraint.ShouldBe(">=3.0.0 <4.0.0");
	}

	[Fact]
	public void GivenPubspecWithoutEnvironmentSection_WhenParsed_ThenSdkConstraintIsNull()
	{
		// Arrange
		const string content = """
		                       name: my_app
		                       dependencies:
		                         http: ^0.13.0
		                       """;

		// Act
		var result = _sut.Parse(content);

		// Assert
		result.SdkConstraint.ShouldBeNull();
	}

	[Theory]
	[InlineData("sdk: '>=2.17.0 <3.0.0'", ">=2.17.0 <3.0.0")]
	[InlineData("sdk: \">=3.0.0 <4.0.0\"", ">=3.0.0 <4.0.0")]
	[InlineData("sdk: any", "any")]
	public void GivenSdkConstraintInVariousFormats_WhenParsed_ThenConstraintIsNormalized(string sdkLine, string expectedConstraint)
	{
		// Arrange
		var content = $"name: my_app\nenvironment:\n  {sdkLine}\n";

		// Act
		var result = _sut.Parse(content);

		// Assert
		result.SdkConstraint.ShouldBe(expectedConstraint);
	}
}
