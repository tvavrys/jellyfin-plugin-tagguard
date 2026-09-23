using Jellyfin.Plugin.TagGuard.Configuration;
using Jellyfin.Plugin.TagGuard.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.TagGuard.Tests;

public sealed class TagSanitizerTests
{
    [Fact]
    public void CreatePlan_WithNoTags_AddsTagsLock()
    {
        var plan = TagSanitizer.CreatePlan([], ["kids"], []);

        Assert.Empty(plan.Tags);
        Assert.Equal([MetadataField.Tags], plan.LockedFields);
        Assert.Equal(0, plan.RemovedTagCount);
        Assert.True(plan.TagsLockAdded);
        Assert.True(plan.RequiresUpdate);
    }

    [Fact]
    public void CreatePlan_WhenAllTagsAllowed_KeepsCanonicalAllowedSpelling()
    {
        var plan = TagSanitizer.CreatePlan(["kids", "Family"], ["Kids", "Family"], []);

        Assert.Equal(["Kids", "Family"], plan.Tags);
        Assert.Equal(0, plan.RemovedTagCount);
        Assert.True(plan.TagsChanged);
    }

    [Fact]
    public void CreatePlan_WhenAllTagsRejected_RemovesEveryTag()
    {
        var plan = TagSanitizer.CreatePlan(["action", "horror"], ["kids"], []);

        Assert.Empty(plan.Tags);
        Assert.Equal(2, plan.RemovedTagCount);
        Assert.True(plan.TagsChanged);
    }

    [Fact]
    public void CreatePlan_WithMixedTags_KeepsOnlyAllowedTags()
    {
        var plan = TagSanitizer.CreatePlan(["kids", "action", "family"], ["kids", "family"], []);

        Assert.Equal(["kids", "family"], plan.Tags);
        Assert.Equal(1, plan.RemovedTagCount);
    }

    [Fact]
    public void CreatePlan_MatchesTagsWithoutCaseSensitivity()
    {
        var plan = TagSanitizer.CreatePlan(["KIDS", "Kids"], ["kids"], []);

        Assert.Equal(["kids"], plan.Tags);
        Assert.Equal(1, plan.RemovedTagCount);
    }

    [Fact]
    public void CreatePlan_CollapsesDuplicateTagsIgnoringCase()
    {
        var plan = TagSanitizer.CreatePlan(["kids", "kids", "KIDS"], ["kids"], []);

        Assert.Equal(["kids"], plan.Tags);
        Assert.Equal(2, plan.RemovedTagCount);
    }

    [Fact]
    public void CreatePlan_TrimsWhitespaceAndDropsBlankValues()
    {
        var plan = TagSanitizer.CreatePlan(["  kids  ", "   ", "action"], [" kids "], []);

        Assert.Equal(["kids"], plan.Tags);
        Assert.Equal(2, plan.RemovedTagCount);
    }

    [Fact]
    public void CreatePlan_WhenTagsAlreadyLocked_DoesNotAddDuplicateLock()
    {
        var plan = TagSanitizer.CreatePlan(["kids"], ["kids"], [MetadataField.Tags]);

        Assert.Equal([MetadataField.Tags], plan.LockedFields);
        Assert.False(plan.TagsLockAdded);
        Assert.False(plan.RequiresUpdate);
    }

    [Fact]
    public void CreatePlan_PreservesUnrelatedLockedFields()
    {
        var existingLocks = new[] { MetadataField.Name, MetadataField.Overview };

        var plan = TagSanitizer.CreatePlan([], ["kids"], existingLocks);

        Assert.Equal([MetadataField.Name, MetadataField.Overview, MetadataField.Tags], plan.LockedFields);
        Assert.True(plan.TagsLockAdded);
    }

    [Fact]
    public void CreatePlan_IsIdempotentAfterSanitization()
    {
        var firstPlan = TagSanitizer.CreatePlan([" KIDS ", "action", "kids"], ["kids"], [MetadataField.Overview]);
        var secondPlan = TagSanitizer.CreatePlan(firstPlan.Tags, ["kids"], firstPlan.LockedFields);

        Assert.Equal(["kids"], secondPlan.Tags);
        Assert.Equal(firstPlan.LockedFields, secondPlan.LockedFields);
        Assert.Equal(0, secondPlan.RemovedTagCount);
        Assert.False(secondPlan.TagsChanged);
        Assert.False(secondPlan.TagsLockAdded);
        Assert.False(secondPlan.RequiresUpdate);
    }

    [Fact]
    public void IsEligible_AcceptsMovieInManagedLibrary()
    {
        var libraryId = Guid.NewGuid();

        Assert.True(TagSanitizer.IsEligible(new Movie(), libraryId, new HashSet<Guid> { libraryId }));
    }

    [Fact]
    public void IsEligible_AcceptsSeriesInManagedLibrary()
    {
        var libraryId = Guid.NewGuid();

        Assert.True(TagSanitizer.IsEligible(new Series(), libraryId, new HashSet<Guid> { libraryId }));
    }

    [Fact]
    public void IsEligible_IgnoresEpisode()
    {
        var libraryId = Guid.NewGuid();

        Assert.False(TagSanitizer.IsEligible(new Episode(), libraryId, new HashSet<Guid> { libraryId }));
    }

    [Fact]
    public void IsEligible_IgnoresItemsOutsideManagedLibraries()
    {
        Assert.False(TagSanitizer.IsEligible(new Movie(), Guid.NewGuid(), new HashSet<Guid> { Guid.NewGuid() }));
    }

    [Fact]
    public void Validate_RejectsMissingAllowlist()
    {
        var library = new CollectionFolder { Id = Guid.NewGuid() };
        var configuration = new PluginConfiguration { ManagedLibraryIds = [library.Id] };

        var result = TagGuardConfigurationValidator.Validate(configuration, [library]);

        Assert.False(result.IsValid);
        Assert.Contains("allowed tag", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsMissingLibrarySelection()
    {
        var configuration = new PluginConfiguration { AllowedTags = ["kids"] };

        var result = TagGuardConfigurationValidator.Validate(configuration, []);

        Assert.False(result.IsValid);
        Assert.Contains("library", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsLibraryIdsThatAreNoLongerPresent()
    {
        var configuration = new PluginConfiguration
        {
            AllowedTags = ["kids"],
            ManagedLibraryIds = [Guid.NewGuid()]
        };

        var result = TagGuardConfigurationValidator.Validate(configuration, []);

        Assert.False(result.IsValid);
        Assert.Contains("no longer exist", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ResolvesExplicitLibraryIds()
    {
        var selectedLibrary = new CollectionFolder { Id = Guid.NewGuid() };
        var unselectedLibrary = new CollectionFolder { Id = Guid.NewGuid() };
        var configuration = new PluginConfiguration
        {
            AllowedTags = [" kids "],
            ManagedLibraryIds = [selectedLibrary.Id]
        };

        var result = TagGuardConfigurationValidator.Validate(configuration, [selectedLibrary, unselectedLibrary]);

        Assert.True(result.IsValid);
        Assert.Equal([selectedLibrary], result.ManagedLibraries);
    }
}
