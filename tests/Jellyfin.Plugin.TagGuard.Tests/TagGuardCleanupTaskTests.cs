using Jellyfin.Plugin.TagGuard.Configuration;
using Jellyfin.Plugin.TagGuard.Services;
using Jellyfin.Plugin.TagGuard.Tasks;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.TagGuard.Tests;

public sealed class TagGuardCleanupTaskTests
{
    [Fact]
    public void GetDefaultTriggers_ReturnsNoScheduledRuns()
    {
        var libraryManager = new Mock<ILibraryManager>().Object;
        var task = new TagGuardCleanupTask(
            new Mock<ILogger<TagGuardCleanupTask>>().Object,
            libraryManager,
            new TagGuardConfigurationValidator(libraryManager),
            new TagSanitizer(libraryManager));

        Assert.Empty(task.GetDefaultTriggers());
    }
}
