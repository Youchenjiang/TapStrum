using System;
using System.IO;
using Tapster.Services;
using Xunit;

namespace Tapster.Core.Tests;

public class AppSettingsServiceTests : IDisposable
{
    private readonly string _testDir;

    public AppSettingsServiceTests()
    {
        _testDir = Path.Join(Path.GetTempPath(), "TapStrumSettingsTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void Load_FileDoesNotExist_ReturnsDefaultSettings()
    {
        var service = new AppSettingsService(_testDir);
        var settings = service.Load();

        Assert.NotNull(settings);
        Assert.Equal("F6", settings.HotkeyClicker);
        Assert.Equal(3.0, settings.DelaySeconds);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsSettings()
    {
        var service = new AppSettingsService(_testDir);
        var data = new AppSettingsData
        {
            HotkeyClicker = "F11",
            DelaySeconds = 7.5,
            Language = "zh-TW",
            ClickerIsKeySpammer = true
        };

        service.Save(data);

        var loaded = service.Load();
        Assert.Equal("F11", loaded.HotkeyClicker);
        Assert.Equal(7.5, loaded.DelaySeconds);
        Assert.Equal("zh-TW", loaded.Language);
        Assert.True(loaded.ClickerIsKeySpammer);
    }

    [Fact]
    public void Load_MigratesFromLegacyFolder()
    {
        string currentFolder = Path.Join(_testDir, "Current");
        string legacyFolder = Path.Join(_testDir, "Legacy");
        Directory.CreateDirectory(legacyFolder);

        string legacyFile = Path.Join(legacyFolder, "settings.json");
        File.WriteAllText(legacyFile, "{\"HotkeyClicker\": \"F12\", \"DelaySeconds\": 2.0}");

        var service = new AppSettingsService(currentFolder, legacyFolder);
        var settings = service.Load();

        Assert.Equal("F12", settings.HotkeyClicker);
        Assert.Equal(2.0, settings.DelaySeconds);

        // Verify it was migrated/saved to currentFolder
        Assert.True(File.Exists(Path.Join(currentFolder, "settings.json")));
    }
}
