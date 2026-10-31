namespace TapStrum.Fluent.Views;

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TapStrum;
using TapStrum.Fluent.ViewModels;

/// <summary>
/// View managing Macro action recording, step editing, and replay execution.
/// </summary>
public sealed partial class MacroView : UserControl
{
    private readonly bool _isInitialized;

    public MacroViewModel? ViewModel
    {
        get => DataContext as MacroViewModel;
        set
        {
            DataContext = value;
            if (value is not null)
            {
                SyncFromViewModel();
            }
        }
    }

    public MacroView()
    {
        InitializeComponent();
        _isInitialized = true;
        Loaded += MacroView_Loaded;
    }

    private void MacroView_Loaded(object sender, RoutedEventArgs e)
    {
        var fmt2 = new Windows.Globalization.NumberFormatting.DecimalFormatter
        {
            IntegerDigits = 1,
            FractionDigits = 2
        };
        var fmtInt = new Windows.Globalization.NumberFormatting.DecimalFormatter
        {
            IntegerDigits = 1,
            FractionDigits = 0
        };

        MacroRepeatBox.NumberFormatter = fmtInt;
        MacroSpeedBox.NumberFormatter = fmt2;
        MacroDelayBox.NumberFormatter = fmtInt;

        MacroRepeatBox.Value = 1;
        MacroSpeedBox.Value = 1.0;
        MacroDelayBox.Value = 3;

        UpdateMacroItemActionBtns();
    }

    private void RecordMacroBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;

        if (ViewModel.IsRecording)
        {
            ViewModel.StopRecording();
            RecordMacroText.Text = LocalizationManager.Get("Macro_RecordStart");
            RecordIcon.Glyph = "\uE7C8";
        }
        else
        {
            ViewModel.StartRecording();
            RecordMacroText.Text = LocalizationManager.Get("Macro_RecordStop");
            RecordIcon.Glyph = "\uE71A";
        }
        SyncFromViewModel();
    }

    private void ClearMacroBtn_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ClearActions();
        SyncFromViewModel();
    }

    private void EditActionBtn_Click(object sender, RoutedEventArgs e)
    {
        // Edit step hook
    }

    private void DeleteActionBtn_Click(object sender, RoutedEventArgs e)
    {
        int idx = MacroActionList.SelectedIndex;
        if (idx >= 0 && ViewModel is not null)
        {
            ViewModel.DeleteAction(idx);
            SyncFromViewModel();
        }
    }

    private void MacroActionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateMacroItemActionBtns();
    }

    private async void MacroActionBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            SyncToViewModel();
            await ViewModel.TogglePlayAsync();
            SyncFromViewModel();
        }
    }

    public void SyncToViewModel()
    {
        if (ViewModel is null) return;

        ViewModel.RepeatLoops = (int)MacroRepeatBox.Value;
        ViewModel.Speed = MacroSpeedBox.Value;
        ViewModel.DelaySeconds = MacroDelayBox.Value;
    }

    public void SyncFromViewModel()
    {
        if (ViewModel is null) return;

        MacroStatusText.Text = ViewModel.StatusText;
        MacroProgressBar.Value = ViewModel.ProgressPercentage;

        MacroActionList.Items.Clear();
        foreach (var action in ViewModel.Actions)
        {
            MacroActionList.Items.Add(action.ToString());
        }

        ClearMacroBtn.IsEnabled = ViewModel.Actions.Count > 0;
        MacroActionBtn.IsEnabled = ViewModel.Actions.Count > 0 || ViewModel.IsRunning;

        if (ViewModel.IsRunning)
        {
            MacroActionText.Text = LocalizationManager.Get("Common_Stop");
            MacroActionIcon.Glyph = "\uE71A";
        }
        else
        {
            MacroActionText.Text = LocalizationManager.Get("Macro_ActionReplay");
            MacroActionIcon.Glyph = "\uE768";
        }

        UpdateMacroItemActionBtns();
    }

    public void UpdateMacroItemActionBtns()
    {
        if (EditActionBtn is null || DeleteActionBtn is null || MacroActionList is null) return;

        int idx = MacroActionList.SelectedIndex;
        bool hasSelection = idx >= 0;
        bool isBusy = ViewModel is { IsRunning: true } or { IsRecording: true };

        EditActionBtn.IsEnabled = hasSelection && !isBusy;
        DeleteActionBtn.IsEnabled = hasSelection && !isBusy;
    }
}
