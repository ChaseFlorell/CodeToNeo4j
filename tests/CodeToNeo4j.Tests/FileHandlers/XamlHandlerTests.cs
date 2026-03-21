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

public class XamlHandlerTests
{
	[Fact]
	public async Task GivenXamlWithElementsAndEvents_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
	{
		// Arrange
		var (sut, fileSystem) = CreateSut();
		var content = @"
<Window x:Class=""MyApp.MainWindow""
        xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <StackPanel x:Name=""MainPanel"">
        <Button x:Name=""SubmitButton"" Click=""SubmitButton_Click"" Content=""Submit"" />
    </StackPanel>
</Window>";
		var filePath = "test.xaml";
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
		var windowSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "Window");
		windowSymbol.ShouldNotBeNull();

		var panelSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "MainPanel");
		panelSymbol.ShouldNotBeNull();

		var buttonSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "SubmitButton");
		buttonSymbol.ShouldNotBeNull();

		var handlerSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "SubmitButton_Click");
		handlerSymbol.ShouldNotBeNull();
		handlerSymbol.Kind.ShouldBe("XamlEventHandler");

		relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == windowSymbol.Key && r.RelType == "CONTAINS");
		relBuffer.ShouldContain(r => r.FromKey == buttonSymbol.Key && r.ToKey == handlerSymbol.Key && r.RelType == "BINDS_TO");
	}

	[Fact]
	public async Task GivenXamlWithLiteralAttributes_WhenHandleCalled_ThenCapturesAsXamlAttribute()
	{
		// Arrange
		var (sut, fileSystem) = CreateSut();
		var content = @"
<ContentPage x:Class=""MyApp.Views.FormView""
             xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
             xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml"">
    <Entry x:Name=""EmailEntry"" Keyboard=""Email"" Placeholder=""Enter email"" />
</ContentPage>";
		var filePath = "test.xaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(null, null, "test-repo", "test-file", filePath, filePath, symbolBuffer, relBuffer, Accessibility.Private);

		// Assert
		var keyboardAttr = symbolBuffer.FirstOrDefault(s => s.Kind == "XamlAttribute" && s.Name == "Keyboard");
		keyboardAttr.ShouldNotBeNull();
		keyboardAttr.Documentation.ShouldBe("Email");
		keyboardAttr.Comments.ShouldBeNull();
		keyboardAttr.Fqn.ShouldBe("Entry.Keyboard=Email");

		var placeholderAttr = symbolBuffer.FirstOrDefault(s => s.Kind == "XamlAttribute" && s.Name == "Placeholder");
		placeholderAttr.ShouldNotBeNull();
		placeholderAttr.Documentation.ShouldBe("Enter email");

		var entrySymbol = symbolBuffer.First(s => s.Kind == "XamlElement" && s.Name == "EmailEntry");
		relBuffer.ShouldContain(r => r.FromKey == entrySymbol.Key && r.ToKey == keyboardAttr.Key && r.RelType == "SETS_PROPERTY");
		relBuffer.ShouldContain(r => r.FromKey == entrySymbol.Key && r.ToKey == placeholderAttr.Key && r.RelType == "SETS_PROPERTY");
	}

	[Fact]
	public async Task GivenXamlWithBindingExpression_WhenHandleCalled_ThenExtractsBindingPath()
	{
		// Arrange
		var (sut, fileSystem) = CreateSut();
		var content = @"
<ContentPage x:Class=""MyApp.Views.FormView""
             xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
             xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml"">
    <Label Text=""{Binding UserName}"" />
    <Entry Text=""{Binding Email, Mode=TwoWay}"" />
</ContentPage>";
		var filePath = "test.xaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(null, null, "test-repo", "test-file", filePath, filePath, symbolBuffer, relBuffer, Accessibility.Private);

		// Assert
		var labelTextAttr = symbolBuffer.FirstOrDefault(s => s.Kind == "XamlAttribute" && s.Name == "Text" && s.Documentation == "{Binding UserName}");
		labelTextAttr.ShouldNotBeNull();
		labelTextAttr.Comments.ShouldBe("UserName");

		var entryTextAttr = symbolBuffer.FirstOrDefault(s => s.Kind == "XamlAttribute" && s.Name == "Text" && s.Documentation == "{Binding Email, Mode=TwoWay}");
		entryTextAttr.ShouldNotBeNull();
		entryTextAttr.Comments.ShouldBe("Email");
	}

	[Fact]
	public async Task GivenXamlWithNamespaceAndXPrefixedAttributes_WhenHandleCalled_ThenSkipsThem()
	{
		// Arrange
		var (sut, fileSystem) = CreateSut();
		var content = @"
<ContentPage x:Class=""MyApp.Views.MyPage""
             xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
             xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
             xmlns:local=""clr-namespace:MyApp.Views""
             Title=""My Page"">
    <Label x:Name=""TitleLabel"" Text=""Hello"" />
</ContentPage>";
		var filePath = "test.xaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(null, null, "test-repo", "test-file", filePath, filePath, symbolBuffer, relBuffer, Accessibility.Private);

		// Assert — namespace declarations and x:-prefixed attributes should NOT appear as XamlAttribute
		symbolBuffer.ShouldNotContain(s => s.Kind == "XamlAttribute" && s.Name == "Class");
		symbolBuffer.ShouldNotContain(s => s.Kind == "XamlAttribute" && s.Name == "Name");
		symbolBuffer.ShouldNotContain(s => s.Kind == "XamlAttribute" && s.Name == "xmlns");
		symbolBuffer.ShouldNotContain(s => s.Kind == "XamlAttribute" && s.Name == "x");
		symbolBuffer.ShouldNotContain(s => s.Kind == "XamlAttribute" && s.Name == "local");

		// Title and Text SHOULD be captured
		symbolBuffer.ShouldContain(s => s.Kind == "XamlAttribute" && s.Name == "Title");
		symbolBuffer.ShouldContain(s => s.Kind == "XamlAttribute" && s.Name == "Text");
	}

	[Fact]
	public async Task GivenXamlWithEventHandlerAttributes_WhenHandleCalled_ThenSkipsThemAsAttributes()
	{
		// Arrange
		var (sut, fileSystem) = CreateSut();
		var content = @"
<ContentPage x:Class=""MyApp.Views.MyPage""
             xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
             xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml"">
    <Button Click=""OnClick"" Content=""Go"" />
</ContentPage>";
		var filePath = "test.xaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(null, null, "test-repo", "test-file", filePath, filePath, symbolBuffer, relBuffer, Accessibility.Private);

		// Assert — Click is an event handler, not an attribute
		symbolBuffer.ShouldNotContain(s => s.Kind == "XamlAttribute" && s.Name == "Click");
		symbolBuffer.ShouldContain(s => s.Kind == "XamlEventHandler" && s.Name == "OnClick");

		// Content IS a property attribute
		symbolBuffer.ShouldContain(s => s.Kind == "XamlAttribute" && s.Name == "Content");
	}

	[Fact]
	public async Task GivenXamlWithPropertyElementChildren_WhenHandleCalled_ThenDoesNotDoubleCount()
	{
		// Arrange
		var (sut, fileSystem) = CreateSut();
		var content = @"
<ContentPage x:Class=""MyApp.Views.MyPage""
             xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
             xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
             xmlns:local=""clr-namespace:MyApp.Views"">
    <Entry x:Name=""MyEntry"" Keyboard=""Email"">
        <Entry.Behaviors>
            <local:MyBehavior />
        </Entry.Behaviors>
    </Entry>
</ContentPage>";
		var filePath = "test.xaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(null, null, "test-repo", "test-file", filePath, filePath, symbolBuffer, relBuffer, Accessibility.Private);

		// Assert — Entry.Behaviors is captured as a child XamlElement, not as an attribute
		var behaviorsElement = symbolBuffer.FirstOrDefault(s => s.Kind == "XamlElement" && s.Name == "Entry.Behaviors");
		behaviorsElement.ShouldNotBeNull();

		// Keyboard attribute is captured
		var keyboardAttr = symbolBuffer.FirstOrDefault(s => s.Kind == "XamlAttribute" && s.Name == "Keyboard");
		keyboardAttr.ShouldNotBeNull();

		// No XamlAttribute named "Behaviors" should exist
		symbolBuffer.ShouldNotContain(s => s.Kind == "XamlAttribute" && s.Name == "Behaviors");
	}

	[Theory]
	[InlineData("{Binding UserName}", "UserName")]
	[InlineData("{Binding Email, Mode=TwoWay}", "Email")]
	[InlineData("{Binding Path=Items.Count}", "Items.Count")]
	[InlineData("{Binding Path=SomeLabel}", "SomeLabel")]
	[InlineData("{Binding Path=SomeLabel, Mode=TwoWay}", "SomeLabel")]
	[InlineData("Plain text value", null)]
	[InlineData("{StaticResource MyStyle}", null)]
	[InlineData("{Binding}", null)]
	public void GivenValue_WhenExtractBindingPath_ThenReturnsExpectedResult(string value, string? expected)
	{
		var result = XamlHandler.ExtractBindingPath(value);
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task GivenXamlWithMultipleAttributesOnElement_WhenHandleCalled_ThenCapturesAll()
	{
		// Arrange
		var (sut, fileSystem) = CreateSut();
		var content = @"
<ContentPage x:Class=""MyApp.Views.MyPage""
             xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
             xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml"">
    <Entry Keyboard=""Email"" Placeholder=""Enter email"" MaxLength=""100"" IsPassword=""False"" />
</ContentPage>";
		var filePath = "test.xaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(null, null, "test-repo", "test-file", filePath, filePath, symbolBuffer, relBuffer, Accessibility.Private);

		// Assert
		List<Symbol> attrSymbols = symbolBuffer.Where(s => s.Kind == "XamlAttribute").ToList();
		attrSymbols.Count.ShouldBe(4);
		attrSymbols.ShouldContain(s => s.Name == "Keyboard");
		attrSymbols.ShouldContain(s => s.Name == "Placeholder");
		attrSymbols.ShouldContain(s => s.Name == "MaxLength");
		attrSymbols.ShouldContain(s => s.Name == "IsPassword");
	}

	[Fact]
	public async Task GivenXamlWithNoAttributes_WhenHandleCalled_ThenNoXamlAttributeSymbols()
	{
		// Arrange
		var (sut, fileSystem) = CreateSut();
		var content = @"
<ContentPage x:Class=""MyApp.Views.MyPage""
             xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
             xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml"">
    <StackLayout>
        <Label />
    </StackLayout>
</ContentPage>";
		var filePath = "test.xaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(null, null, "test-repo", "test-file", filePath, filePath, symbolBuffer, relBuffer, Accessibility.Private);

		// Assert
		symbolBuffer.ShouldNotContain(s => s.Kind == "XamlAttribute");
		relBuffer.ShouldNotContain(r => r.RelType == "SETS_PROPERTY");
	}

	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".xaml"], "xaml"));
		return fake;
	}

	private static (XamlHandler sut, MockFileSystem fileSystem) CreateSut()
	{
		MockFileSystem fileSystem = new();
		SymbolMapper symbolMapper = new();
		MemberDependencyExtractor dependencyExtractor = new(symbolMapper);
		RoslynSymbolProcessor symbolProcessor = new(symbolMapper, dependencyExtractor);
		XamlHandler sut = new(symbolProcessor, fileSystem, new TextSymbolMapper(), NullLogger<XamlHandler>.Instance, CreateConfigService());
		return (sut, fileSystem);
	}
}
