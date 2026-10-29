namespace TapStrum.Fluent.Tests;

using TapStrum.Services;
using TapStrum.Fluent.ViewModels;
using Xunit;

public class SettingsViewModelTests
{
    private sealed class MockSettingsService : IAppSettingsService
    {
        public AppSettingsData Current { get; set; } = new();
        public bool SaveCalled { get; private set; }
        public bool LoadCalled { get; private set; }

        public AppSettingsData Load()
        {
            LoadCalled = true;
            return Current;
        }

        public void Save(AppSettingsData settings)
        {
            SaveCalled = true;
            Current = settings;
        }
    }

    [Fact]
    public void LoadAndSave_SynchronizesSettingsWithService()
    {
        var mock = new MockSettingsService
        {
            Current = new AppSettingsData
            {
                Language = "zh-TW",
                HotkeyClicker = "F11",
                DelaySeconds = 5.0
            }
        };

        var vm = new SettingsViewModel(mock);
        Assert.Equal("zh-TW", vm.Language);
        Assert.Equal("F11", vm.HotkeyClicker);
        Assert.Equal(5.0, vm.DelaySeconds);

        vm.Language = "en-US";
        vm.Save();

        Assert.True(mock.SaveCalled);
        Assert.Equal("en-US", mock.Current.Language);
    }

    [Fact]
    public void ResetToDefaults_RestoresDefaultConfiguration()
    {
        var mock = new MockSettingsService();
        var vm = new SettingsViewModel(mock)
        {
            Language = "ja-JP",
            DelaySeconds = 10.0
        };

        vm.ResetToDefaults();

        Assert.Equal("auto", vm.Language);
        Assert.Equal(3.0, vm.DelaySeconds);
        Assert.True(mock.SaveCalled);
    }
}
