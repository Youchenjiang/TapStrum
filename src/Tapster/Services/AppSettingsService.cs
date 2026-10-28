using System;
using System.IO;
using System.Text.Json;

namespace Tapster.Services;

/// <summary>
/// Default implementation of <see cref="IAppSettingsService"/> storing settings in local application data.
/// </summary>
public sealed class AppSettingsService : IAppSettingsService
{
    private readonly string _settingsFolder;
    private readonly string _settingsFilePath;
    private readonly string _legacySettingsFilePath;
    private AppSettingsData? _current;

    /// <summary>
    /// Initializes a new instance of <see cref="AppSettingsService"/> with default LocalApplicationData storage.
    /// </summary>
    public AppSettingsService()
        : this(
            settingsFolder: Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TapStrum"),
            legacyFolder: Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tapster"))
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AppSettingsService"/> with custom folders for testing.
    /// </summary>
    public AppSettingsService(string settingsFolder, string? legacyFolder = null)
    {
        _settingsFolder = settingsFolder ?? throw new ArgumentNullException(nameof(settingsFolder));
        _settingsFilePath = Path.Join(_settingsFolder, "settings.json");
        _legacySettingsFilePath = legacyFolder != null
            ? Path.Join(legacyFolder, "settings.json")
            : string.Empty;
    }

    /// <inheritdoc/>
    public AppSettingsData Current => _current ??= Load();

    /// <inheritdoc/>
    public AppSettingsData Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettingsData>(json);
                if (settings != null)
                {
                    _current = settings;
                    return settings;
                }
            }

            if (!string.IsNullOrEmpty(_legacySettingsFilePath) && File.Exists(_legacySettingsFilePath))
            {
                string legacyJson = File.ReadAllText(_legacySettingsFilePath);
                var legacySettings = JsonSerializer.Deserialize<AppSettingsData>(legacyJson);
                if (legacySettings != null)
                {
                    _current = legacySettings;
                    Save(legacySettings);
                    return legacySettings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }

        var defaults = new AppSettingsData();
        _current = defaults;
        return defaults;
    }

    /// <inheritdoc/>
    public void Save(AppSettingsData settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            if (!Directory.Exists(_settingsFolder))
            {
                Directory.CreateDirectory(_settingsFolder);
            }

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
            _current = settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}
