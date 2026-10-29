#pragma warning disable S2325 // WinUI XAML event handlers and controls are generated in partial class
namespace Tapster_Fluent.Views;

using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Tapster;
using Tapster_Fluent.ViewModels;

/// <summary>
/// View managing the Auto-Typer input, mode configuration, and action execution.
/// </summary>
public sealed partial class TyperView : UserControl
{
    public TyperViewModel? ViewModel
    {
        get => DataContext as TyperViewModel;
        set
        {
            DataContext = value;
            if (value is not null)
            {
                SyncFromViewModel();
            }
        }
    }

    public TyperView()
    {
        InitializeComponent();
        Loaded += TyperView_Loaded;
    }

    private void TyperView_Loaded(object sender, RoutedEventArgs e)
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

        TypeIntervalBox.NumberFormatter = fmt2;
        TyperDelayBox.NumberFormatter = fmtInt;

        TypeIntervalBox.Value = 0.03;
        TyperDelayBox.Value = 3;

        UpdateTyperActionBtnState();
    }

    private async void Paste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                string text = await dataPackageView.GetTextAsync();
                TypeTextBox.Text = text;
                UpdateTyperActionBtnState();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to paste text: {ex.Message}");
        }
    }

    private void ClearText_Click(object sender, RoutedEventArgs e)
    {
        TypeTextBox.Text = string.Empty;
        UpdateTyperActionBtnState();
    }

    private void TypeTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateTyperActionBtnState();
    }

    private void TypeModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool isClipboard = TypeModeCombo.SelectedIndex == 1;
        TypeModeHint.Visibility = isClipboard ? Visibility.Visible : Visibility.Collapsed;
        TypeIntervalPanel.Visibility = isClipboard ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void TyperActionBtn_Click(object sender, RoutedEventArgs e)
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

        ViewModel.Text = TypeTextBox.Text;
        ViewModel.IsClipboardMode = TypeModeCombo.SelectedIndex == 1;
        ViewModel.TrailingKeyIndex = TypeTrailingKeyCombo.SelectedIndex;
        ViewModel.IntervalSeconds = TypeIntervalBox.Value;
        ViewModel.JitterEnabled = TypeJitterCheck.IsChecked.GetValueOrDefault();
        ViewModel.DelaySeconds = TyperDelayBox.Value;
    }

    public void SyncFromViewModel()
    {
        if (ViewModel is null) return;

        TyperStatusText.Text = ViewModel.StatusText;
        TyperProgressBar.Value = ViewModel.ProgressPercentage;

        if (ViewModel.IsRunning)
        {
            TyperActionText.Text = LocalizationManager.Get("Common_Stop");
            TyperActionIcon.Glyph = "\uE71A";
        }
        else
        {
            TyperActionText.Text = LocalizationManager.Get("Typer_ActionStart");
            TyperActionIcon.Glyph = "\uE768";
        }

        UpdateTyperActionBtnState();
    }

    public void UpdateTyperActionBtnState()
    {
        bool hasText = !string.IsNullOrWhiteSpace(TypeTextBox.Text);
        ClearTextBtn.IsEnabled = hasText;

        if (ViewModel is { IsRunning: true })
        {
            TyperActionBtn.IsEnabled = true;
            return;
        }

        TyperActionBtn.IsEnabled = hasText;
    }
}
