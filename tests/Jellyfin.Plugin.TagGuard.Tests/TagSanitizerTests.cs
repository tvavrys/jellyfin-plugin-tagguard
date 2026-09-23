using Jellyfin.Plugin.TagGuard.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.TagGuard.Tests;

public sealed class TagSanitizerTests
{
    private readonly TagSanitizer _sanitizer = new();

    [Fact]
    public void CreatePlan_WithNoTags_AddsTagsLock()
    {
        var plan = _sanitizer.CreatePlan([], ["kids"], []);

        Assert.Empty(plan.Tags);
        Assert.Equal([MetadataField.Tags], plan.LockedFields);
        Assert.Equal(0, plan.RemovedTagCount);
        Assert.True(plan.TagsLockAdded);
        Assert.True(plan.RequiresUpdate);
    }

    [Fact]
    public void CreatePlan_WhenAllTagsAllowed_KeepsCanonicalAllowedSpelling()
    {
        var plan = _sanitizer.CreatePlan(["kids", "Family"], ["Kids", "Family"], []);

        Assert.Equal(["Kids", "Family"], plan.Tags);
        Assert.Equal(0, plan.RemovedTagCount);
        Assert.True(plan.TagsChanged);
    }

    [Fact]
    public void CreatePlan_WhenAllTagsRejected_RemovesEveryTag()
    {
        var plan = _sanitizer.CreatePlan(["action", "horror"], ["kids"], []);

        Assert.Empty(plan.Tags);
        Assert.Equal(2, plan.RemovedTagCount);
        Assert.True(plan.TagsChanged);
    }

    [Fact]
    public void CreatePlan_WithMixedTags_KeepsOnlyAllowedTags()
    {
        var plan = _sanitizer.CreatePlan(["kids", "action", "family"], ["kids", "family"], []);

        Assert.Equal(["kids", "family"], plan.Tags);
        Assert.Equal(1, plan.RemovedTagCount);
    }

    [Fact]
    public void CreatePlan_MatchesTagsWithoutCaseSensitivity()
    {
        var plan = _sanitizer.CreatePlan(["KIDS", "Kids"], ["kids"], []);

        Assert.Equal(["kids"], plan.Tags);
        Assert.Equal(1, plan.RemovedTagCount);
    }

    [Fact]
    public void CreatePlan_CollapsesDuplicateTagsIgnoringCase()
    {
        var plan = _sanitizer.CreatePlan(["kids", "kids", "KIDS"], ["kids"], []);

        Assert.Equal(["kids"], plan.Tags);
        Assert.Equal(2, plan.RemovedTagCount);
    }

    [Fact]
    public void CreatePlan_TrimsWhitespaceAndDropsBlankValues()
    {
        var plan = _sanitizer.CreatePlan(["  kids  ", "   ", "action"], [" kids "], []);

        Assert.Equal(["kids"], plan.Tags);
        Assert.Equal(2, plan.RemovedTagCount);
    }

    [Fact]
    public void CreatePlan_WhenTagsAlreadyLocked_DoesNotAddDuplicateLock()
    {
        var plan = _sanitizer.CreatePlan(["kids"], ["kids"], [MetadataField.Tags]);

        Assert.Equal([MetadataField.Tags], plan.LockedFields);
        Assert.False(plan.TagsLockAdded);
        Assert.False(plan.RequiresUpdate);
    }

    [Fact]
    public void CreatePlan_PreservesUnrelatedLockedFields()
    {
        var existingLocks = new[] { MetadataField.Name, MetadataField.Overview };

        var plan = _sanitizer.CreatePlan([], ["kids"], existingLocks);

        Assert.Equal([MetadataField.Name, MetadataField.Overview, MetadataField.Tags], plan.LockedFields);
        Assert.True(plan.TagsLockAdded);
    }

    [Fact]
    public void CreatePlan_IsIdempotentAfterSanitization()
    {
        var firstPlan = _sanitizer.CreatePlan([" KIDS ", "action", "kids"], ["kids"], [MetadataField.Overview]);
        var secondPlan = _sanitizer.CreatePlan(firstPlan.Tags, ["kids"], firstPlan.LockedFields);

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
}
