namespace TapStrum.Services;

/// <summary>
/// Domain service interface for reading and persisting application configuration data.
/// </summary>
public interface IAppSettingsService
{
    /// <summary>
    /// Gets the current cached application settings instance.
    /// </summary>
    AppSettingsData Current { get; }

    /// <summary>
    /// Loads settings from disk storage, falling back to defaults or legacy locations.
    /// </summary>
    AppSettingsData Load();

    /// <summary>
    /// Persists settings to disk storage.
    /// </summary>
    /// <param name="settings">The settings instance to save.</param>
    void Save(AppSettingsData settings);
}
