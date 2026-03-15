using CodeToNeo4j.VersionControl;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.VersionControl;

public class GitMetadataCacheTests
{
    [Fact]
    public void GivenEmptyCache_WhenTryGetCalled_ThenReturnsFalse()
    {
        // Arrange
        var sut = new GitMetadataCache();

        // Act
        var found = sut.TryGet("/some/file.cs", out _);

        // Assert
        found.ShouldBeFalse();
    }

    [Fact]
    public void GivenCachedEntry_WhenTryGetCalled_ThenReturnsTrueWithMetadata()
    {
        // Arrange
        var sut = new GitMetadataCache();
        var metadata = new FileMetadata(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []);
        sut.Set("/some/file.cs", metadata);

        // Act
        var found = sut.TryGet("/some/file.cs", out var result);

        // Assert
        found.ShouldBeTrue();
        result.ShouldBe(metadata);
    }

    [Fact]
    public void GivenCaseInsensitivePaths_WhenTryGetCalled_ThenMatchesRegardlessOfCase()
    {
        // Arrange
        var sut = new GitMetadataCache();
        var metadata = new FileMetadata(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []);
        sut.Set("/Some/FILE.cs", metadata);

        // Act
        var found = sut.TryGet("/some/file.cs", out var result);

        // Assert
        found.ShouldBeTrue();
        result.ShouldBe(metadata);
    }

    [Fact]
    public void GivenPopulatedCache_WhenClearCalled_ThenCacheIsEmpty()
    {
        // Arrange
        var sut = new GitMetadataCache();
        sut.Set("/a.cs", new FileMetadata(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []));
        sut.Set("/b.cs", new FileMetadata(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []));

        // Act
        sut.Clear();

        // Assert
        sut.Count.ShouldBe(0);
        sut.TryGet("/a.cs", out _).ShouldBeFalse();
    }

    [Fact]
    public void GivenMultipleEntries_WhenCountAccessed_ThenReturnsCorrectCount()
    {
        // Arrange
        var sut = new GitMetadataCache();
        sut.Set("/a.cs", new FileMetadata(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []));
        sut.Set("/b.cs", new FileMetadata(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []));

        // Act & Assert
        sut.Count.ShouldBe(2);
    }
}
