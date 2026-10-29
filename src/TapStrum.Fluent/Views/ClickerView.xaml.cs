#pragma warning disable S2325 // WinUI XAML event handlers and controls are generated in partial class
namespace TapStrum.Fluent.Views;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TapStrum;
using TapStrum.Fluent.ViewModels;

/// <summary>
/// View managing the Auto-Clicker and Key-Spammer configuration and execution pipeline.
/// </summary>
public sealed partial class ClickerView : UserControl
{
    public ClickerViewModel? ViewModel
    {
        get => DataContext as ClickerViewModel;
        set
        {
            DataContext = value;
            if (value is not null)
            {
                SyncFromViewModel();
            }
        }
    }

    public ClickerView()
    {
        InitializeComponent();
        Loaded += ClickerView_Loaded;
    }

    private void ClickerView_Loaded(object sender, RoutedEventArgs e)
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

        ClickIntervalBox.NumberFormatter = fmt2;
        ClickCountBox.NumberFormatter = fmtInt;
        TimeJitterBox.NumberFormatter = fmtInt;
        LocationJitterBox.NumberFormatter = fmtInt;
        ClickerDelayBox.NumberFormatter = fmtInt;

        ClickIntervalBox.Value = 0.1;
        ClickCountBox.Value = 100;
        ClickerDelayBox.Value = 3;

        ClickTriggerModeCombo.SelectedIndex = AppSettings.Current.ClickerHoldMode ? 1 : 0;
        ClickTargetTypeCombo.SelectedIndex = AppSettings.Current.ClickerIsKeySpammer ? 1 : 0;
        SpamKeyBox.Text = string.IsNullOrEmpty(AppSettings.Current.ClickerSpamKey) ? "space" : AppSettings.Current.ClickerSpamKey;

        TimeJitterCheck.IsChecked = AppSettings.Current.ClickerJitterEnabled;
        TimeJitterBox.IsEnabled = AppSettings.Current.ClickerJitterEnabled;
        TimeJitterBox.Value = AppSettings.Current.ClickerTimeJitterPercent;
        LocationJitterBox.Value = AppSettings.Current.ClickerLocationJitterPx;
        ShowTargetMarkerCheck.IsChecked = AppSettings.Current.ClickerShowTargetMarker;

        UpdateClickerActionBtnState();
        UpdateClearCoordsBtnState();
    }

    private void ClickTargetTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool isSpammer = ClickTargetTypeCombo.SelectedIndex == 1;

        MouseButtonLabel.Visibility = isSpammer ? Visibility.Collapsed : Visibility.Visible;
        MouseButtonCombo.Visibility = isSpammer ? Visibility.Collapsed : Visibility.Visible;

        SpamKeyLabel.Visibility = isSpammer ? Visibility.Visible : Visibility.Collapsed;
        SpamKeyBox.Visibility = isSpammer ? Visibility.Visible : Visibility.Collapsed;

        ClickCoordsLabel.Visibility = isSpammer ? Visibility.Collapsed : Visibility.Visible;
        ClickCoordsPanel.Visibility = isSpammer ? Visibility.Collapsed : Visibility.Visible;
        LocationJitterLabel.Visibility = isSpammer ? Visibility.Collapsed : Visibility.Visible;
        LocationJitterPanel.Visibility = isSpammer ? Visibility.Collapsed : Visibility.Visible;

        AppSettings.Current.ClickerIsKeySpammer = isSpammer;
        AppSettings.Current.Save();

        UpdateClickerActionBtnState();
    }

    private void ClickTriggerModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool isHold = ClickTriggerModeCombo.SelectedIndex == 1;
        HoldModeHint.Visibility = isHold ? Visibility.Visible : Visibility.Collapsed;

        AppSettings.Current.ClickerHoldMode = isHold;
        AppSettings.Current.Save();
    }

    private void SpamKeyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        AppSettings.Current.ClickerSpamKey = SpamKeyBox.Text;
        AppSettings.Current.Save();
        UpdateClickerActionBtnState();
    }

    private void TimeJitterCheck_Checked(object sender, RoutedEventArgs e)
    {
        TimeJitterBox.IsEnabled = true;
        AppSettings.Current.ClickerJitterEnabled = true;
        AppSettings.Current.Save();
    }

    private void TimeJitterCheck_Unchecked(object sender, RoutedEventArgs e)
    {
        TimeJitterBox.IsEnabled = false;
        AppSettings.Current.ClickerJitterEnabled = false;
        AppSettings.Current.Save();
    }

    private void ClickCoords_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        UpdateClearCoordsBtnState();
    }

    private async void PickCoordBtn_Click(object sender, RoutedEventArgs e)
    {
        ClickerStatusText.Text = LocalizationManager.Get("Clicker_PickingCoordsStatus");

        var (picked, canceled, x, y) = await Task.Run(PollTargetCoordinatesAsync, CancellationToken.None);

        PickCoordText.Text = LocalizationManager.Get("Clicker_PickCoordBtn");
        if (canceled || !picked)
        {
            ClickerStatusText.Text = LocalizationManager.Get("Clicker_PickCoordsCanceled");
            return;
        }

        ClickXBox.Value = x;
        ClickYBox.Value = y;
        ShowTargetMarkerCheck.IsChecked = true;
        UpdateClearCoordsBtnState();
        ClickerStatusText.Text = string.Format(LocalizationManager.Get("Clicker_PickCoordsLocked"), x, y);
    }

    private void ClearCoordsBtn_Click(object sender, RoutedEventArgs e)
    {
        ClickXBox.Value = double.NaN;
        ClickYBox.Value = double.NaN;
        UpdateClearCoordsBtnState();
        ClickerStatusText.Text = LocalizationManager.Get("Clicker_CoordsCleared");
    }

    private static async Task<(bool Picked, bool Canceled, int X, int Y)> PollTargetCoordinatesAsync()
    {
        const int VK_SPACE = 0x20;
        const int VK_RETURN = 0x0D;
        const int VK_LBUTTON = 0x01;
        const int VK_ESCAPE = 0x1B;

        await Task.Delay(250);
        var start = DateTime.UtcNow;

        while ((DateTime.UtcNow - start).TotalSeconds < 4.0)
        {
            if ((NativeMethods.GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
            {
                return (false, true, 0, 0);
            }

            bool triggered = (NativeMethods.GetAsyncKeyState(VK_SPACE) & 0x8000) != 0 ||
                             (NativeMethods.GetAsyncKeyState(VK_RETURN) & 0x8000) != 0 ||
                             (NativeMethods.GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;

            if (triggered)
            {
                var (x, y) = Mouse.GetPosition();
                return (true, false, x, y);
            }

            await Task.Delay(20);
        }

        var (fx, fy) = Mouse.GetPosition();
        return (true, false, fx, fy);
    }

    private async void ClickerActionBtn_Click(object sender, RoutedEventArgs e)
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

        ViewModel.IsKeySpammer = ClickTargetTypeCombo.SelectedIndex == 1;
        ViewModel.SpamKey = SpamKeyBox.Text;
        ViewModel.IsHoldMode = ClickTriggerModeCombo.SelectedIndex == 1;
        ViewModel.MouseButtonIndex = MouseButtonCombo.SelectedIndex;
        ViewModel.IntervalSeconds = ClickIntervalBox.Value;
        ViewModel.TotalClicks = (int)ClickCountBox.Value;
        ViewModel.TargetX = double.IsNaN(ClickXBox.Value) ? null : ClickXBox.Value;
        ViewModel.TargetY = double.IsNaN(ClickYBox.Value) ? null : ClickYBox.Value;
        ViewModel.JitterEnabled = TimeJitterCheck.IsChecked.GetValueOrDefault();
        ViewModel.TimeJitterPercent = TimeJitterBox.Value;
        ViewModel.LocationJitterPx = LocationJitterBox.Value;
        ViewModel.ShowTargetMarker = ShowTargetMarkerCheck.IsChecked.GetValueOrDefault();
        ViewModel.DelaySeconds = ClickerDelayBox.Value;
    }

    public void SyncFromViewModel()
    {
        if (ViewModel is null) return;

        ClickerStatusText.Text = ViewModel.StatusText;
        ClickerProgressBar.Value = ViewModel.ProgressPercentage;

        if (ViewModel.IsRunning)
        {
            ClickerActionText.Text = LocalizationManager.Get("Common_Stop");
            ClickerActionIcon.Glyph = "\uE71A";
        }
        else
        {
            ClickerActionText.Text = LocalizationManager.Get("Clicker_ActionStart");
            ClickerActionIcon.Glyph = "\uE768";
        }

        UpdateClickerActionBtnState();
    }

    public void UpdateClickerActionBtnState()
    {
        if (ViewModel is { IsRunning: true })
        {
            ClickerActionBtn.IsEnabled = true;
            return;
        }

        bool isSpammer = ClickTargetTypeCombo.SelectedIndex == 1;
        ClickerActionBtn.IsEnabled = !isSpammer || !string.IsNullOrWhiteSpace(SpamKeyBox.Text);
    }

    public void UpdateClearCoordsBtnState()
    {
        bool hasCoords = !double.IsNaN(ClickXBox.Value) || !double.IsNaN(ClickYBox.Value);
        ClearCoordsBtn.IsEnabled = hasCoords;
    }
}
