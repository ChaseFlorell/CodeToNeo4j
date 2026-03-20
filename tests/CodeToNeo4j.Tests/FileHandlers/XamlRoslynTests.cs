using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class XamlRoslynTests
{
	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".xaml"], "xaml"));
		return fake;
	}

	[Fact]
	public async Task GivenXamlWithGeneratedCode_WhenHandleCalled_ThenExtractsMembersViaRoslyn()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor);
		XamlHandler sut = new(symbolProcessor, fileSystem, new TextSymbolMapper(), NullLogger<XamlHandler>.Instance, CreateConfigService());

		var xamlFilePath = "MainWindow.xaml";
		var xamlContent = @"<Window x:Class=""MyApp.MainWindow""
        xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Button x:Name=""SubmitButton"" />
</Window>";
		fileSystem.AddFile(xamlFilePath, new(xamlContent));

		// Simulate generated C# code from XAML (simplified)
		var generatedCode = @"
namespace MyApp
{
#line 1 ""MainWindow.xaml""
    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector
    {
#line 4 ""MainWindow.xaml""
        internal System.Windows.Controls.Button SubmitButton;
#line default
    }
}";
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
			.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

		var generatedDoc = workspace.AddDocument(project.Id, "MainWindow.g.cs", SourceText.From(generatedCode));
		var compilation = await generatedDoc.Project.GetCompilationAsync();

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			compilation,
			"test-repo",
			"MainWindow.xaml",
			xamlFilePath,
			xamlFilePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		// Should find the class and the generated field
		var classSymbol = symbolBuffer.FirstOrDefault(s => s is { Name: "MainWindow", Kind: "NamedType" });
		classSymbol.ShouldNotBeNull();

		var fieldSymbol = symbolBuffer.FirstOrDefault(s => s is { Name: "SubmitButton", Kind: "Field" });
		fieldSymbol.ShouldNotBeNull();
		fieldSymbol.StartLine.ShouldBe(4); // Mapped line
	}
}
