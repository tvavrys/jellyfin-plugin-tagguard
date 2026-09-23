using Jellyfin.Plugin.TagGuard.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.TagGuard.Tests;

public sealed class TagSanitizerPersistenceTests
{
    [Fact]
    public async Task SanitizeAsync_SkipsEpisodeItems()
    {
        var libraryManager = new Mock<ILibraryManager>();
        var sanitizer = new TagSanitizer(libraryManager.Object);

        var result = await sanitizer.SanitizeAsync(new Episode { Tags = ["action"] }, ["kids"], CancellationToken.None);

        Assert.True(result.WasSkipped);
        Assert.False(result.Changed);
        libraryManager.Verify(manager => manager.UpdateItemAsync(It.IsAny<BaseItem>(), It.IsAny<BaseItem>(), It.IsAny<ItemUpdateType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SanitizeAsync_PersistsTagsAndLocksThroughLibraryManager()
    {
        var parentId = Guid.NewGuid();
        var parent = new Folder();
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            Name = "Example",
            Tags = [" action ", "KIDS"],
            LockedFields = [MetadataField.Name]
        };
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetItemById(parentId)).Returns(parent);
        var sanitizer = new TagSanitizer(libraryManager.Object);
        libraryManager
            .Setup(manager => manager.UpdateItemAsync(movie, parent, ItemUpdateType.MetadataEdit, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Assert.True(sanitizer.IsPersistingUpdate(movie.Id));
                return Task.CompletedTask;
            });

        var result = await sanitizer.SanitizeAsync(movie, ["kids"], CancellationToken.None);

        Assert.True(result.Changed);
        Assert.Equal(1, result.TagsRemoved);
        Assert.True(result.TagsLockAdded);
        Assert.Equal(["kids"], movie.Tags);
        Assert.Equal([MetadataField.Name, MetadataField.Tags], movie.LockedFields);
        libraryManager.Verify(manager => manager.UpdateItemAsync(movie, parent, ItemUpdateType.MetadataEdit, It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(sanitizer.IsPersistingUpdate(movie.Id));
    }

    [Fact]
    public async Task SanitizeAsync_RestoresInMemoryFieldsWhenPersistenceFails()
    {
        var parentId = Guid.NewGuid();
        var parent = new Folder();
        var originalTags = new[] { "action" };
        var originalLockedFields = new[] { MetadataField.Name };
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            Tags = originalTags,
            LockedFields = originalLockedFields
        };
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetItemById(parentId)).Returns(parent);
        libraryManager
            .Setup(manager => manager.UpdateItemAsync(movie, parent, ItemUpdateType.MetadataEdit, It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(new InvalidOperationException("simulated persistence failure")));
        var sanitizer = new TagSanitizer(libraryManager.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sanitizer.SanitizeAsync(movie, ["kids"], CancellationToken.None));

        Assert.Same(originalTags, movie.Tags);
        Assert.Same(originalLockedFields, movie.LockedFields);
        Assert.False(sanitizer.IsPersistingUpdate(movie.Id));
    }
}
