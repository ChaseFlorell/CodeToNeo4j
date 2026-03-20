using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;
using CodeToNeo4j.Configuration;
using FakeItEasy;

namespace CodeToNeo4j.Tests.FileHandlers;

public class TypeScriptHandlerTests
{
	[Theory]
	[InlineData("test.ts")]
	[InlineData("test.tsx")]
	public void GivenTsOrTsxFile_WhenCanHandleCalled_ThenReturnsTrue(string filePath)
	{
		TypeScriptHandler sut = new(new MockFileSystem(), new TextSymbolMapper(), new ConfigurationService());
		sut.CanHandle(filePath).ShouldBeTrue();
	}

	[Fact]
	public void GivenJsFile_WhenCanHandleCalled_ThenReturnsFalse()
	{
		TypeScriptHandler sut = new(new MockFileSystem(), new TextSymbolMapper(), new ConfigurationService());
		sut.CanHandle("test.js").ShouldBeFalse();
	}

	[Fact]
	public async Task GivenTsInSubfolder_WhenHandleCalled_ThenNamespaceIsDirectory()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		TypeScriptHandler sut = new(fileSystem, new TextSymbolMapper(), new ConfigurationService());
		var filePath = "src/utils/test.ts";
		fileSystem.AddFile(filePath, new("function foo() {}"));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null,
			null,
			"test-repo",
			"test.ts",
			filePath,
			filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		result.Namespace.ShouldBe("src/utils");
		symbolBuffer.First().Namespace.ShouldBe("src/utils");
	}

	[Fact]
	public async Task GivenTsWithFunctionAndImport_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		TypeScriptHandler sut = new(fileSystem, new TextSymbolMapper(), new ConfigurationService());
		var content = @"
import { Component } from '@angular/core';
function myFunction(value: string): void {
    console.log(value);
}
const myArrow = (x: number): number => x * 2;";
		var filePath = "test.ts";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"test.ts",
			filePath,
			filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert — @angular/core is a bare module specifier, so it links directly to pkg: without creating a symbol
		symbolBuffer.ShouldNotContain(s => s.Kind == "TypeScriptImport");
		relBuffer.ShouldContain(r => r.FromKey == "test.ts" && r.ToKey == "pkg:@angular/core" && r.RelType == "DEPENDS_ON");

		var functionSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "myFunction");
		functionSymbol.ShouldNotBeNull();
		functionSymbol.Kind.ShouldBe("TypeScriptFunction");

		var arrowSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "myArrow");
		arrowSymbol.ShouldNotBeNull();
		arrowSymbol.Kind.ShouldBe("TypeScriptFunction");

		relBuffer.ShouldContain(r => r.FromKey == "test.ts" && r.ToKey == functionSymbol.Key && r.RelType == "CONTAINS");
	}

	[Fact]
	public async Task GivenTsWithInterface_WhenHandleCalled_ThenAddsInterfaceSymbol()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		TypeScriptHandler sut = new(fileSystem, new TextSymbolMapper(), new ConfigurationService());
		var content = @"
interface User {
    id: number;
    name: string;
}";
		var filePath = "test.ts";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"test.ts",
			filePath,
			filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		var interfaceSymbol = symbolBuffer.FirstOrDefault(s => s.Kind == "TypeScriptInterface");
		interfaceSymbol.ShouldNotBeNull();
		interfaceSymbol.Name.ShouldBe("User");
		interfaceSymbol.Class.ShouldBe("interface");

		relBuffer.ShouldContain(r => r.FromKey == "test.ts" && r.ToKey == interfaceSymbol.Key && r.RelType == "CONTAINS");
	}

	[Fact]
	public async Task GivenTsWithTypeAlias_WhenHandleCalled_ThenAddsTypeAliasSymbol()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		TypeScriptHandler sut = new(fileSystem, new TextSymbolMapper(), new ConfigurationService());
		var content = @"
type UserId = string | number;
type Result<T> = { value: T; error?: string };";
		var filePath = "test.ts";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"test.ts",
			filePath,
			filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		List<Symbol> typeAliases = symbolBuffer.Where(s => s.Kind == "TypeScriptTypeAlias").ToList();
		typeAliases.Count.ShouldBe(2);
		typeAliases.ShouldContain(s => s.Name == "UserId");
		typeAliases.ShouldContain(s => s.Name == "Result");
	}

	[Fact]
	public async Task GivenTsWithEnum_WhenHandleCalled_ThenAddsEnumSymbol()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		TypeScriptHandler sut = new(fileSystem, new TextSymbolMapper(), new ConfigurationService());
		var content = @"
enum Direction {
    Up,
    Down,
    Left,
    Right
}
const enum Status {
    Active = 'active',
    Inactive = 'inactive'
}";
		var filePath = "test.ts";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"test.ts",
			filePath,
			filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		List<Symbol> enums = symbolBuffer.Where(s => s.Kind == "TypeScriptEnum").ToList();
		enums.Count.ShouldBe(2);
		enums.ShouldContain(s => s.Name == "Direction");
		enums.ShouldContain(s => s.Name == "Status");
		enums.ForEach(e => e.Class.ShouldBe("enum"));

		relBuffer.ShouldContain(r => r.RelType == "CONTAINS" && enums.Any(e => e.Key == r.ToKey));
	}

	[Fact]
	public async Task GivenTsWithFunctionCallGraph_WhenHandleCalled_ThenAddsInvokesRelationship()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		TypeScriptHandler sut = new(fileSystem, new TextSymbolMapper(), new ConfigurationService());
		var content = @"
function validate(order: Order): boolean {
    return order != null;
}
function processOrder(order: Order): void {
    validate(order);
    save(order);
}
function save(order: Order): void {}";
		var filePath = "test.ts";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"test.ts",
			filePath,
			filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		var processOrder = symbolBuffer.FirstOrDefault(s => s.Name == "processOrder");
		var validate = symbolBuffer.FirstOrDefault(s => s.Name == "validate");
		var save = symbolBuffer.FirstOrDefault(s => s.Name == "save");

		processOrder.ShouldNotBeNull();
		validate.ShouldNotBeNull();
		save.ShouldNotBeNull();

		relBuffer.ShouldContain(r => r.FromKey == processOrder!.Key && r.ToKey == validate!.Key && r.RelType == "INVOKES");
		relBuffer.ShouldContain(r => r.FromKey == processOrder!.Key && r.ToKey == save!.Key && r.RelType == "INVOKES");
	}

	[Fact]
	public async Task GivenTsWithMixedConstructs_WhenHandleCalled_ThenExtractsAll()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		TypeScriptHandler sut = new(fileSystem, new TextSymbolMapper(), new ConfigurationService());
		var content = @"
import { Injectable } from '@angular/core';

interface UserService {
    getUser(id: string): Promise<User>;
}

type UserId = string;

enum Role {
    Admin,
    User
}

function createUser(name: string): User {
    return { name };
}";
		var filePath = "test.ts";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"test.ts",
			filePath,
			filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		// @angular/core is a bare module specifier — links directly to pkg: node, no symbol created
		relBuffer.ShouldContain(r => r.ToKey == "pkg:@angular/core" && r.RelType == "DEPENDS_ON");
		symbolBuffer.ShouldContain(s => s.Kind == "TypeScriptInterface" && s.Name == "UserService");
		symbolBuffer.ShouldContain(s => s.Kind == "TypeScriptTypeAlias" && s.Name == "UserId");
		symbolBuffer.ShouldContain(s => s.Kind == "TypeScriptEnum" && s.Name == "Role");
		symbolBuffer.ShouldContain(s => s.Kind == "TypeScriptFunction" && s.Name == "createUser");
	}

	[Fact]
	public async Task GivenTsWithNoConstructs_WhenHandleCalled_ThenNoSymbols()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		TypeScriptHandler sut = new(fileSystem, new TextSymbolMapper(), new ConfigurationService());
		var content = "// just a comment\nconst x = 42;";
		var filePath = "test.ts";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"test.ts",
			filePath,
			filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		symbolBuffer.ShouldBeEmpty();
		relBuffer.ShouldBeEmpty();
	}
}
