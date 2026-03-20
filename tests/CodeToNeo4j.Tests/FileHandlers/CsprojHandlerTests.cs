using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class CsprojHandlerTests
{
	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".csproj"], "xml"));
		return fake;
	}

	[Fact]
	public async Task GivenCsprojWithPackageAndProjectReferences_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CsprojHandler sut = new(fileSystem, new TextSymbolMapper(), NullLogger<CsprojHandler>.Instance, CreateConfigService());

		var content = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.1"" />
    <ProjectReference Include=""..\MyLib\MyLib.csproj"" />
  </ItemGroup>
</Project>";
		var filePath = "test.csproj";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"test-file",
			filePath, filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		var propertySymbol = symbolBuffer.FirstOrDefault(s => s.Name == "TargetFramework");
		propertySymbol.ShouldNotBeNull();
		propertySymbol.Kind.ShouldBe("ProjectProperty");
		propertySymbol.Documentation.ShouldBe("net8.0");

		var packageSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "Newtonsoft.Json");
		packageSymbol.ShouldNotBeNull();
		packageSymbol.Key.ShouldBe("pkg:Newtonsoft.Json");
		packageSymbol.Kind.ShouldBe("Dependency");
		packageSymbol.Documentation.ShouldBe("13.0.1");
		packageSymbol.Version.ShouldBe("13.0.1");

		var projectSymbol = symbolBuffer.FirstOrDefault(s => s.Name == @"..\MyLib\MyLib.csproj");
		projectSymbol.ShouldNotBeNull();
		projectSymbol.Kind.ShouldBe("ProjectReference");

		relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == propertySymbol.Key && r.RelType == "HAS_PROPERTY");
		relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == packageSymbol.Key && r.RelType == "DEPENDS_ON");
		relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == projectSymbol.Key && r.RelType == "DEPENDS_ON");
	}

	[Fact]
	public async Task GivenPackageReference_AndNuspecHasBothUrls_WhenHandled_ThenReturnsUrlNodesInFileResult()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CsprojHandler sut = new(fileSystem, new TextSymbolMapper(), NullLogger<CsprojHandler>.Instance, CreateConfigService());

		const string content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.1"" />
  </ItemGroup>
</Project>";
		const string filePath = "test.csproj";
		fileSystem.AddFile(filePath, new(content));

		const string nuspecContent = """
		                             <?xml version="1.0"?>
		                             <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
		                               <metadata>
		                                 <projectUrl>https://www.newtonsoft.com/json</projectUrl>
		                                 <repository type="git" url="https://github.com/JamesNK/Newtonsoft.Json" />
		                               </metadata>
		                             </package>
		                             """;
		fileSystem.AddFile(NuspecPath("Newtonsoft.Json", "13.0.1"), new(nuspecContent));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null, null,
			"test-repo", "test-file",
			filePath, filePath,
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert — URL data is in FileResult, not in the symbol/rel buffers
		result.UrlNodes.ShouldNotBeNull();
		result.UrlNodes.Count.ShouldBe(2);

		var projectUrlNode = result.UrlNodes.FirstOrDefault(u => u.Name == "https://www.newtonsoft.com/json");
		projectUrlNode.ShouldNotBeNull();
		projectUrlNode.DepKey.ShouldBe("pkg:Newtonsoft.Json");
		projectUrlNode.UrlKey.ShouldBe("url:https://www.newtonsoft.com/json");

		var repoUrlNode = result.UrlNodes.FirstOrDefault(u => u.Name == "https://github.com/JamesNK/Newtonsoft.Json");
		repoUrlNode.ShouldNotBeNull();
		repoUrlNode.DepKey.ShouldBe("pkg:Newtonsoft.Json");
		repoUrlNode.UrlKey.ShouldBe("url:https://github.com/JamesNK/Newtonsoft.Json");

		// URL data must NOT appear in the symbol or relationship buffers
		symbolBuffer.ShouldNotContain(s => s.Kind == "Url");
		relBuffer.ShouldNotContain(r => r.RelType == "HAS_URL");
	}

	[Fact]
	public async Task GivenPackageReference_AndNuspecHasOnlyProjectUrl_WhenHandled_ThenReturnsOneUrlNode()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CsprojHandler sut = new(fileSystem, new TextSymbolMapper(), NullLogger<CsprojHandler>.Instance, CreateConfigService());

		const string content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Serilog"" Version=""3.0.0"" />
  </ItemGroup>
</Project>";
		const string filePath = "test.csproj";
		fileSystem.AddFile(filePath, new(content));

		const string nuspecContent = """
		                             <?xml version="1.0"?>
		                             <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
		                               <metadata>
		                                 <projectUrl>https://serilog.net</projectUrl>
		                               </metadata>
		                             </package>
		                             """;
		fileSystem.AddFile(NuspecPath("Serilog", "3.0.0"), new(nuspecContent));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null, null,
			"test-repo", "test-file",
			filePath, filePath,
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert
		result.UrlNodes.ShouldNotBeNull();
		result.UrlNodes.Count.ShouldBe(1);
		result.UrlNodes.First().UrlKey.ShouldBe("url:https://serilog.net");
	}

	[Fact]
	public async Task GivenPackageReference_AndNuspecHasOnlyRepositoryUrl_WhenHandled_ThenReturnsOneUrlNode()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CsprojHandler sut = new(fileSystem, new TextSymbolMapper(), NullLogger<CsprojHandler>.Instance, CreateConfigService());

		const string content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""MyLib"" Version=""1.0.0"" />
  </ItemGroup>
</Project>";
		const string filePath = "test.csproj";
		fileSystem.AddFile(filePath, new(content));

		const string nuspecContent = """
		                             <?xml version="1.0"?>
		                             <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
		                               <metadata>
		                                 <repository type="git" url="https://github.com/org/mylib" />
		                               </metadata>
		                             </package>
		                             """;
		fileSystem.AddFile(NuspecPath("MyLib", "1.0.0"), new(nuspecContent));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null, null,
			"test-repo", "test-file",
			filePath, filePath,
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert
		result.UrlNodes.ShouldNotBeNull();
		result.UrlNodes.Count.ShouldBe(1);
		result.UrlNodes.First().UrlKey.ShouldBe("url:https://github.com/org/mylib");
	}

	[Fact]
	public async Task GivenPackageReference_AndNuspecIsMissing_WhenHandled_ThenNoUrlNodes()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CsprojHandler sut = new(fileSystem, new TextSymbolMapper(), NullLogger<CsprojHandler>.Instance, CreateConfigService());

		const string content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""SomePackage"" Version=""2.0.0"" />
  </ItemGroup>
</Project>";
		const string filePath = "test.csproj";
		fileSystem.AddFile(filePath, new(content));
		// No nuspec added to MockFileSystem

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null, null,
			"test-repo", "test-file",
			filePath, filePath,
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert
		result.UrlNodes.ShouldBeNull();
	}

	[Fact]
	public async Task GivenPackageReference_AndNuspecIsMalformed_WhenHandled_ThenNoUrlNodesAndNoException()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CsprojHandler sut = new(fileSystem, new TextSymbolMapper(), NullLogger<CsprojHandler>.Instance, CreateConfigService());

		const string content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""BrokenPkg"" Version=""1.0.0"" />
  </ItemGroup>
</Project>";
		const string filePath = "test.csproj";
		fileSystem.AddFile(filePath, new(content));
		fileSystem.AddFile(NuspecPath("BrokenPkg", "1.0.0"), new("this is not valid xml <<>>"));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act — should not throw
		var result = await sut.Handle(
			null, null,
			"test-repo", "test-file",
			filePath, filePath,
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert
		result.UrlNodes.ShouldBeNull();
	}

	[Fact]
	public async Task GivenPackageReferenceWithNoVersion_WhenHandled_ThenNoUrlNodes()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CsprojHandler sut = new(fileSystem, new TextSymbolMapper(), NullLogger<CsprojHandler>.Instance, CreateConfigService());

		const string content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""SomePackage"" />
  </ItemGroup>
</Project>";
		const string filePath = "test.csproj";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null, null,
			"test-repo", "test-file",
			filePath, filePath,
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert
		result.UrlNodes.ShouldBeNull();
	}

	[Fact]
	public async Task GivenXmlWithNoRootElement_WhenHandled_ThenReturnsEmptyWithoutThrowing()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CsprojHandler sut = new(fileSystem, new TextSymbolMapper(), NullLogger<CsprojHandler>.Instance, CreateConfigService());

		// A well-formed XML document with no root element (just a processing instruction)
		const string content = "<?xml version=\"1.0\"?>";
		const string filePath = "test.csproj";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act — Root is null, should skip processing gracefully
		var exception = await Record.ExceptionAsync(() => sut.Handle(
			null, null,
			"test-repo", "test-file",
			filePath, filePath,
			symbolBuffer, relBuffer,
			Accessibility.Private));

		// Assert
		exception.ShouldBeNull();
		symbolBuffer.ShouldBeEmpty();
		relBuffer.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenMalformedCsproj_WhenHandled_ThenReturnsEmptyWithoutThrowing()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CsprojHandler sut = new(fileSystem, new TextSymbolMapper(), NullLogger<CsprojHandler>.Instance, CreateConfigService());

		const string filePath = "test.csproj";
		fileSystem.AddFile(filePath, new("<<< not xml >>>"));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var exception = await Record.ExceptionAsync(() => sut.Handle(
			null, null,
			"test-repo", "test-file",
			filePath, filePath,
			symbolBuffer, relBuffer,
			Accessibility.Private));

		// Assert
		exception.ShouldBeNull();
		symbolBuffer.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("<Project><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>", "net9.0")]
	[InlineData("<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>", "net8.0")]
	public async Task GivenCsprojWithSingleTargetFramework_WhenHandled_ThenFileResultContainsTfm(string content, string expectedTfm)
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CsprojHandler sut = new(fileSystem, new TextSymbolMapper(), NullLogger<CsprojHandler>.Instance, CreateConfigService());

		const string filePath = "test.csproj";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null, null,
			"test-repo", "test-file",
			filePath, filePath,
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert
		result.TargetFrameworks.ShouldNotBeNull();
		result.TargetFrameworks.ShouldContain(expectedTfm);
	}

	[Fact]
	public async Task GivenCsprojWithMultipleTargetFrameworks_WhenHandled_ThenFileResultContainsAllTfms()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CsprojHandler sut = new(fileSystem, new TextSymbolMapper(), NullLogger<CsprojHandler>.Instance, CreateConfigService());

		const string content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
  </PropertyGroup>
</Project>";
		const string filePath = "test.csproj";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null, null,
			"test-repo", "test-file",
			filePath, filePath,
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert
		result.TargetFrameworks.ShouldNotBeNull();
		result.TargetFrameworks.ShouldContain("net8.0");
		result.TargetFrameworks.ShouldContain("net9.0");
	}

	[Fact]
	public async Task GivenCsprojWithNoTargetFramework_WhenHandled_ThenFileResultHasNullTfms()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CsprojHandler sut = new(fileSystem, new TextSymbolMapper(), NullLogger<CsprojHandler>.Instance, CreateConfigService());

		const string content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""SomePackage"" Version=""1.0.0"" />
  </ItemGroup>
</Project>";
		const string filePath = "test.csproj";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null, null,
			"test-repo", "test-file",
			filePath, filePath,
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert
		result.TargetFrameworks.ShouldBeNull();
	}

	private static string NuspecPath(string name, string version)
	{
		var packagesRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
						   ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
		var nameNormalized = name.ToLowerInvariant();
		return Path.Combine(packagesRoot, nameNormalized, version, $"{nameNormalized}.nuspec");
	}
}
