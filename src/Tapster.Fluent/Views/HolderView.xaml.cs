namespace Tapster_Fluent.Views;

using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tapster;
using Tapster_Fluent.ViewModels;

/// <summary>
/// View managing the multi-stage Key-Holder pipeline and virtual keyboard configuration.
/// </summary>
public sealed partial class HolderView : UserControl
{
    public HolderViewModel? ViewModel
    {
        get => DataContext as HolderViewModel;
        set
        {
            DataContext = value;
            if (value is not null)
            {
                SyncFromViewModel();
            }
        }
    }

    public StackPanel StepsContainer => HolderStepsContainer;
    public StackPanel VirtualKeyboardContainer => KeyboardContainer;
    public TextBlock StepTargetHint => ActiveStepTargetHint;
    public StackPanel RepeatPanel => HolderRepeatPanel;
    public NumberBox RepeatBox => HolderRepeatBox;
    public ScrollViewer ScrollViewer => HolderScrollViewer;

    public HolderView()
    {
        InitializeComponent();
        Loaded += HolderView_Loaded;
    }

    private void HolderView_Loaded(object sender, RoutedEventArgs e)
    {
        var fmtInt = new Windows.Globalization.NumberFormatting.DecimalFormatter
        {
            IntegerDigits = 1,
            FractionDigits = 0
        };

        HolderRepeatBox.NumberFormatter = fmtInt;
        HolderDelayBox.NumberFormatter = fmtInt;

        HolderRepeatBox.Value = 1;
        HolderDelayBox.Value = 3;

        UpdateHolderActionBtnState();
    }

    private void AddStepBtn_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.AddStage(new KeyHolderStage());
        SyncFromViewModel();
    }

    private void CaptureKeyBtn_Click(object sender, RoutedEventArgs e)
    {
        // Physical key capture
    }

    private void ClearActiveStepKeysBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.ClearStageKeys(ViewModel.ActiveStageIndex);
            SyncFromViewModel();
        }
    }

    private async void HolderActionBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            SyncToViewModel();
            await ViewModel.ToggleAsync();
            SyncFromViewModel();
        }
    }

    public void SyncToViewModel()
    {
        if (ViewModel is null) return;

        ViewModel.RepeatLoops = (int)HolderRepeatBox.Value;
        ViewModel.DelaySeconds = HolderDelayBox.Value;
    }

    public void SyncFromViewModel()
    {
        if (ViewModel is null) return;

        HolderStatusText.Text = ViewModel.StatusText;
        HolderProgressBar.Value = ViewModel.ProgressPercentage;

        if (ViewModel.IsRunning)
        {
            HolderActionText.Text = LocalizationManager.Get("Common_Stop");
            HolderActionIcon.Glyph = "\uE71A";
        }
        else
        {
            HolderActionText.Text = LocalizationManager.Get("KeyHolder_ActionStart");
            HolderActionIcon.Glyph = "\uE768";
        }

        UpdateHolderActionBtnState();
    }

    public void UpdateHolderActionBtnState()
    {
        if (ViewModel is { IsRunning: true })
        {
            HolderActionBtn.IsEnabled = true;
            return;
        }

        HolderActionBtn.IsEnabled = ViewModel is not null && ViewModel.Stages.Any(s => !string.IsNullOrWhiteSpace(s.KeyCombo));
    }
}
