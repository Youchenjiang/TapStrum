namespace Tapster_Fluent.ViewModels;

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tapster;

/// <summary>
/// Root ViewModel coordinating navigation, global status, and child automation ViewModels.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    public const string TabTyper = "Typer";
    public const string TabHolder = "Holder";
    public const string TabClicker = "Clicker";
    public const string TabMacro = "Macro";
    public const string TabSettings = "Settings";
    public const string TabAbout = "About";

    [ObservableProperty]
    private string _selectedNavigationTag = TabTyper;

    [ObservableProperty]
    private bool _isAnyTaskRunning;

    [ObservableProperty]
    private string? _runningTaskName;

    [ObservableProperty]
    private string _globalStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isAlwaysOnTop;

    public ClickerViewModel Clicker { get; }
    public HolderViewModel Holder { get; }
    public TyperViewModel Typer { get; }
    public MacroViewModel Macro { get; }
    public SettingsViewModel Settings { get; }

    public MainViewModel(
        ClickerViewModel? clicker = null,
        HolderViewModel? holder = null,
        TyperViewModel? typer = null,
        MacroViewModel? macro = null,
        SettingsViewModel? settings = null,
        Action<Action>? dispatcher = null)
        : base(dispatcher)
    {
        Clicker = clicker ?? new ClickerViewModel(dispatcher: dispatcher);
        Holder = holder ?? new HolderViewModel(dispatcher: dispatcher);
        Typer = typer ?? new TyperViewModel(dispatcher: dispatcher);
        Macro = macro ?? new MacroViewModel(dispatcher: dispatcher);
        Settings = settings ?? new SettingsViewModel(dispatcher: dispatcher);

        Clicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ClickerViewModel.IsRunning))
            {
                OnChildRunningChanged(TabClicker, Clicker.IsRunning);
            }
        };

        Holder.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HolderViewModel.IsRunning))
            {
                OnChildRunningChanged(TabHolder, Holder.IsRunning);
            }
        };

        Typer.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TyperViewModel.IsRunning))
            {
                OnChildRunningChanged(TabTyper, Typer.IsRunning);
            }
        };

        Macro.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MacroViewModel.IsRunning))
            {
                OnChildRunningChanged(TabMacro, Macro.IsRunning);
            }
        };
    }

    private void OnChildRunningChanged(string taskName, bool isRunning)
    {
        RunOnUIThread(() =>
        {
            if (isRunning)
            {
                IsAnyTaskRunning = true;
                RunningTaskName = taskName;
                GlobalStatusMessage = string.Format(LocalizationManager.Get("Common_Running"), taskName);
            }
            else if (RunningTaskName == taskName)
            {
                IsAnyTaskRunning = false;
                RunningTaskName = null;
                GlobalStatusMessage = LocalizationManager.Get("Common_Finished");
            }
        });
    }

    [RelayCommand]
    public void StopAll(string? reason = null)
    {
        string message = reason ?? LocalizationManager.Get("Common_StoppedByUser");

        if (Clicker.IsRunning)
        {
            Clicker.Stop(message);
        }

        if (Holder.IsRunning)
        {
            Holder.Stop(message);
        }

        if (Typer.IsRunning)
        {
            Typer.Stop(message);
        }

        if (Macro.IsRunning)
        {
            Macro.Stop(message);
        }

        IsAnyTaskRunning = false;
        RunningTaskName = null;
        GlobalStatusMessage = message;
    }
}
