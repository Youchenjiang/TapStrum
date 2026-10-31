namespace TapStrum.Fluent.Views;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TapStrum;
using TapStrum.Fluent.ViewModels;

/// <summary>
/// View managing the multi-stage Key-Holder pipeline and virtual keyboard configuration.
/// </summary>
public sealed partial class HolderView : UserControl
{
    private const string KeyTab = "tab";
    private const string KeyEnter = "enter";
    private const string KeyPrintScreen = "printscreen";
    private const string AccentFillBrush = "AccentFillColorDefaultBrush";

    private readonly Dictionary<string, List<Button>> _keyboardButtons = new(StringComparer.OrdinalIgnoreCase);
    private bool _isCapturingKey;
    private bool _isInitialized;

    public HolderViewModel? ViewModel
    {
        get => DataContext as HolderViewModel;
        set
        {
            if (DataContext is HolderViewModel oldVm)
            {
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            }

            DataContext = value;
            if (value is not null)
            {
                value.PropertyChanged += OnViewModelPropertyChanged;
                if (value.Stages.Count == 0 && _isInitialized)
                {
                    value.AddStage(new KeyHolderStage("space", 0, 0));
                }
                SyncFromViewModel();
                RenderHolderStepCards();
                UpdateVirtualKeyboardHighlights();
            }
        }
    }

    public HolderView()
    {
        InitializeComponent();
        Loaded += HolderView_Loaded;
        LocalizationManager.LanguageChanged += ApplyLocalization;
        _isInitialized = true;
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

        HolderRepeatBox.Value = ViewModel?.RepeatLoops ?? 1;
        HolderDelayBox.Value = ViewModel?.DelaySeconds ?? 3;

        if (ViewModel != null && ViewModel.Stages.Count == 0)
        {
            ViewModel.AddStage(new KeyHolderStage("space", 0, 0));
        }

        HolderScrollViewer.SizeChanged += (_, _) => UpdateKeyboardContainerWidth();
        GenerateVirtualKeyboard();
        UpdateKeyboardContainerWidth();
        RenderHolderStepCards();
        UpdateVirtualKeyboardHighlights();
        ApplyLocalization();
        UpdateHolderActionBtnState();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (ViewModel is null) return;

        switch (e.PropertyName)
        {
            case nameof(HolderViewModel.RunningStageIndex):
                RenderHolderStepCards();
                break;
            case nameof(HolderViewModel.ActiveStageIndex):
                RenderHolderStepCards();
                UpdateVirtualKeyboardHighlights();
                break;
            case nameof(HolderViewModel.IsRunning):
                SyncFromViewModel();
                RenderHolderStepCards();
                break;
            case nameof(HolderViewModel.StatusText):
                HolderStatusText.Text = ViewModel.StatusText;
                break;
            case nameof(HolderViewModel.ProgressPercentage):
                HolderProgressBar.Value = ViewModel.ProgressPercentage;
                break;
        }
    }

    private void UpdateKeyboardContainerWidth()
    {
        if (KeyboardContainer == null || HolderScrollViewer == null) return;
        double availableWidth = HolderScrollViewer.ActualWidth - 36;
        KeyboardContainer.Width = Math.Max(804, availableWidth);
    }

    private void AddStepBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.AddStage(new KeyHolderStage("", 0, 0));
        RenderHolderStepCards();
        UpdateVirtualKeyboardHighlights();
        UpdateHolderActionBtnState();
        UpdateHolderRepeatPanelVisibility();
    }

    private async void CaptureKeyBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isCapturingKey)
        {
            _isCapturingKey = false;
            CaptureIcon.Glyph = "\uE7C8";
            CaptureKeyText.Text = LocalizationManager.Get("KeyHolder_CaptureBtn");
            HolderStatusText.Text = LocalizationManager.Get("KeyHolder_CaptureCanceled");
            return;
        }

        _isCapturingKey = true;
        CaptureIcon.Glyph = "\uE71A";
        CaptureKeyText.Text = LocalizationManager.Get("KeyHolder_CaptureListening");
        HolderStatusText.Text = LocalizationManager.Get("KeyHolder_CaptureHint");

        await Task.Run(async () =>
        {
            await Task.Delay(200);

            while (_isCapturingKey)
            {
                await Task.Delay(15);
                int vk = PollForPressedVirtualKey();
                if (vk != 0)
                {
                    string k = Keyboard.GetKeyName(vk);
                    DispatcherQueue.TryEnqueue(() => ApplyCapturedKey(k));
                    return;
                }
            }
        }, CancellationToken.None);
    }

    private void ApplyCapturedKey(string k)
    {
        if (ViewModel is not null && ViewModel.ActiveStageIndex >= 0 && ViewModel.ActiveStageIndex < ViewModel.Stages.Count)
        {
            ViewModel.Stages[ViewModel.ActiveStageIndex].KeyCombo = k;
            RenderHolderStepCards();
            UpdateVirtualKeyboardHighlights();
            UpdateHolderActionBtnState();
        }
        _isCapturingKey = false;
        CaptureIcon.Glyph = "\uE7C8";
        CaptureKeyText.Text = LocalizationManager.Get("KeyHolder_CaptureBtn");
        HolderStatusText.Text = string.Format(LocalizationManager.Get("KeyHolder_CaptureSuccess"), k, (ViewModel?.ActiveStageIndex ?? 0) + 1);
    }

    private static int PollForPressedVirtualKey()
    {
        for (int vk = 0x08; vk <= 0xFE; vk++)
        {
            short state = NativeMethods.GetAsyncKeyState(vk);
            if ((state & 0x8000) != 0 || state < 0)
            {
                return vk;
            }
        }
        return 0;
    }

    private void ClearActiveStepKeysBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.ClearStageKeys(ViewModel.ActiveStageIndex);
            RenderHolderStepCards();
            UpdateVirtualKeyboardHighlights();
            UpdateHolderActionBtnState();
            HolderStatusText.Text = string.Format(LocalizationManager.Get("KeyHolder_KeysClearedForStep"), ViewModel.ActiveStageIndex + 1);
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

    private void UpdateHolderRepeatPanelVisibility()
    {
        if (HolderRepeatPanel == null) return;
        bool show = ViewModel != null && (ViewModel.Stages.Count > 1 || ViewModel.Stages.Any(s => s.HoldDurationSec > 0));
        HolderRepeatPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Virtual Keyboard Generation & Key Highlighting
    // ══════════════════════════════════════════════════════════════════════════════

    private void GenerateVirtualKeyboard()
    {
        KeyboardContainer.Children.Clear();
        _keyboardButtons.Clear();

        // ── Main Keyboard Panel (Rows 0 to 5) ──
        var mainKeyboardPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // Row 0: Function Keys
        var row0 = CreateKeyboardRow();
        AddKeyBtn(row0, "esc", "Esc", width: 44);
        AddSpacer(row0, 8);
        AddKeyBtn(row0, "f1", "F1"); AddKeyBtn(row0, "f2", "F2"); AddKeyBtn(row0, "f3", "F3"); AddKeyBtn(row0, "f4", "F4");
        AddSpacer(row0, 10);
        AddKeyBtn(row0, "f5", "F5"); AddKeyBtn(row0, "f6", "F6"); AddKeyBtn(row0, "f7", "F7"); AddKeyBtn(row0, "f8", "F8");
        AddSpacer(row0, 10);
        AddKeyBtn(row0, "f9", "F9"); AddKeyBtn(row0, "f10", "F10"); AddKeyBtn(row0, "f11", "F11"); AddKeyBtn(row0, "f12", "F12");
        AddSpacer(row0, 28);
        mainKeyboardPanel.Children.Add(row0);

        // Row 1: Number Row & Backspace
        var row1 = CreateKeyboardRow();
        string[] r1Keys = { "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" };
        foreach (var k in r1Keys) AddKeyBtn(row1, k, k);
        AddKeyBtn(row1, "backspace", "Backspace", width: 76);
        mainKeyboardPanel.Children.Add(row1);

        // Row 2: QWERTY Row & \
        var row2 = CreateKeyboardRow();
        AddKeyBtn(row2, KeyTab, "Tab", width: 54);
        string[] r2Keys = { "q", "w", "e", "r", "t", "y", "u", "i", "o", "p", "[", "]" };
        foreach (var k in r2Keys) AddKeyBtn(row2, k, k.ToUpperInvariant());
        AddKeyBtn(row2, "\\", "\\", width: 58);
        mainKeyboardPanel.Children.Add(row2);

        // Row 3: Home Row & Enter
        var row3 = CreateKeyboardRow();
        AddKeyBtn(row3, "capslock", "Caps Lock", width: 66);
        string[] r3Keys = { "a", "s", "d", "f", "g", "h", "j", "k", "l", ";", "'" };
        foreach (var k in r3Keys) AddKeyBtn(row3, k, k.ToUpperInvariant());
        AddKeyBtn(row3, KeyEnter, "Enter", width: 86);
        mainKeyboardPanel.Children.Add(row3);

        // Row 4: Shift Row & Up Arrow
        var row4 = CreateKeyboardRow();
        AddKeyBtn(row4, "shift", "Shift", width: 88);
        string[] r4Keys = { "z", "x", "c", "v", "b", "n", "m", ",", ".", "/" };
        foreach (var k in r4Keys) AddKeyBtn(row4, k, k.ToUpperInvariant());
        AddKeyBtn(row4, "shift", "Shift", width: 64);
        AddKeyBtn(row4, "up", "▲");
        mainKeyboardPanel.Children.Add(row4);

        // Row 5: Bottom Row (Ctrl, Win, Alt, Space, Alt, Win, Ctrl, Left Arrow, Down Arrow)
        var row5 = CreateKeyboardRow();
        AddKeyBtn(row5, "ctrl", "Ctrl", width: 48);
        AddKeyBtn(row5, "win", "Win", width: 44);
        AddKeyBtn(row5, "alt", "Alt", width: 44);
        AddKeyBtn(row5, "space", "Space", width: 242);
        AddKeyBtn(row5, "alt", "Alt", width: 38);
        AddKeyBtn(row5, "win", "Win", width: 38);
        AddKeyBtn(row5, "ctrl", "Ctrl", width: 38);
        AddKeyBtn(row5, "left", "◄");
        AddKeyBtn(row5, "down", "▼");
        mainKeyboardPanel.Children.Add(row5);

        // ── Right Numpad Grid (Rows 0 to 5 x Columns 0 to 4) ──
        var numpadGrid = new Grid
        {
            RowSpacing = 4,
            ColumnSpacing = 4,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        for (int r = 0; r < 6; r++)
        {
            numpadGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
        }
        for (int c = 0; c < 5; c++)
        {
            numpadGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        }

        // Row 0: Top control & navigation keys
        AddGridKeyBtn(numpadGrid, 0, 0, KeyPrintScreen, "PrtSc");
        AddGridKeyBtn(numpadGrid, 0, 1, "scrolllock", "ScrLk");
        AddGridKeyBtn(numpadGrid, 0, 2, "pause", "Pause");
        AddGridKeyBtn(numpadGrid, 0, 3, "insert", "Ins");
        AddGridKeyBtn(numpadGrid, 0, 4, "delete", "Del");

        // Row 1: Numpad operators & Home
        AddGridKeyBtn(numpadGrid, 1, 0, "numlock", "NumLk");
        AddGridKeyBtn(numpadGrid, 1, 1, "divide", "/");
        AddGridKeyBtn(numpadGrid, 1, 2, "multiply", "*");
        AddGridKeyBtn(numpadGrid, 1, 3, "subtract", "-");
        AddGridKeyBtn(numpadGrid, 1, 4, "home", "Home");

        // Row 2: 7 8 9 + PgUp (+ spans Row 2 & 3: 72px)
        AddGridKeyBtn(numpadGrid, 2, 0, "num7", "7");
        AddGridKeyBtn(numpadGrid, 2, 1, "num8", "8");
        AddGridKeyBtn(numpadGrid, 2, 2, "num9", "9");
        AddSpannedGridKeyBtn(numpadGrid, 2, 3, "add", "+");
        AddGridKeyBtn(numpadGrid, 2, 4, "pageup", "PgUp");

        // Row 3: 4 5 6 PgDn (+ spans from row 2)
        AddGridKeyBtn(numpadGrid, 3, 0, "num4", "4");
        AddGridKeyBtn(numpadGrid, 3, 1, "num5", "5");
        AddGridKeyBtn(numpadGrid, 3, 2, "num6", "6");
        AddGridKeyBtn(numpadGrid, 3, 4, "pagedown", "PgDn");

        // Row 4: 1 2 3 NumpadEnter End (Enter and End span Row 4 & 5: 72px)
        AddGridKeyBtn(numpadGrid, 4, 0, "num1", "1");
        AddGridKeyBtn(numpadGrid, 4, 1, "num2", "2");
        AddGridKeyBtn(numpadGrid, 4, 2, "num3", "3");
        AddSpannedGridKeyBtn(numpadGrid, 4, 3, "numpadenter", "Enter");
        AddSpannedGridKeyBtn(numpadGrid, 4, 4, "end", "End");

        // Row 5: ► 0 . (Enter and End span from row 4)
        AddGridKeyBtn(numpadGrid, 5, 0, "right", "►");
        AddGridKeyBtn(numpadGrid, 5, 1, "num0", "0");
        AddGridKeyBtn(numpadGrid, 5, 2, "decimal", ".");

        // ── Master Dual-Column Grid ──
        var masterGrid = new Grid
        {
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        masterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        masterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(mainKeyboardPanel, 0);
        Grid.SetColumn(numpadGrid, 1);

        masterGrid.Children.Add(mainKeyboardPanel);
        masterGrid.Children.Add(numpadGrid);

        KeyboardContainer.Children.Add(masterGrid);

        UpdateVirtualKeyboardHighlights();
    }

    private static StackPanel CreateKeyboardRow()
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }

    private static void AddSpacer(StackPanel row, double width)
    {
        row.Children.Add(new Border { Width = width });
    }

    private Button CreateKeyBtn(string keyId, string label, double width = 36, double height = 34)
    {
        var btn = new Button
        {
            Content = label,
            Width = width,
            Height = height,
            Padding = new Thickness(2),
            FontSize = width > 50 || height > 50 ? 10 : 11,
            FontFamily = new FontFamily("Cascadia Code, Consolas"),
            CornerRadius = new CornerRadius(4),
            Style = Application.Current.Resources["DefaultButtonStyle"] as Style
        };

        btn.Click += (s, e) =>
        {
            if (ViewModel is null) return;

            if (ViewModel.Stages.Count == 0)
            {
                ViewModel.AddStage(new KeyHolderStage("", 0, 0));
            }
            if (ViewModel.ActiveStageIndex < 0 || ViewModel.ActiveStageIndex >= ViewModel.Stages.Count)
            {
                ViewModel.ActiveStageIndex = 0;
            }

            var stage = ViewModel.Stages[ViewModel.ActiveStageIndex];
            string current = stage.KeyCombo.Trim();
            if (string.IsNullOrEmpty(current))
            {
                stage.KeyCombo = keyId;
            }
            else
            {
                var keys = Keyboard.SplitCombo(current).ToList();
                if (keys.Any(k => IsKeyMatch(k, keyId)))
                {
                    keys.RemoveAll(k => string.Equals(k, keyId, StringComparison.OrdinalIgnoreCase));
                    stage.KeyCombo = string.Join("+", keys);
                }
                else
                {
                    keys.Add(keyId);
                    stage.KeyCombo = string.Join("+", keys);
                }
            }
            RenderHolderStepCards();
            UpdateVirtualKeyboardHighlights();
            UpdateHolderActionBtnState();
        };

        if (!_keyboardButtons.TryGetValue(keyId, out var btnList))
        {
            btnList = [];
            _keyboardButtons[keyId] = btnList;
        }
        btnList.Add(btn);
        return btn;
    }

    private void AddKeyBtn(StackPanel row, string keyId, string label, double width = 36)
    {
        var btn = CreateKeyBtn(keyId, label, width, 34);
        row.Children.Add(btn);
    }

    private void AddGridKeyBtn(Grid grid, int row, int col, string keyId, string label)
    {
        var btn = CreateKeyBtn(keyId, label, 36, 34);
        Grid.SetRow(btn, row);
        Grid.SetColumn(btn, col);
        grid.Children.Add(btn);
    }

    private void AddSpannedGridKeyBtn(Grid grid, int row, int col, string keyId, string label, int rowSpan = 2)
    {
        var btn = CreateKeyBtn(keyId, label, 36, 72);
        Grid.SetRow(btn, row);
        Grid.SetColumn(btn, col);
        Grid.SetRowSpan(btn, rowSpan);
        grid.Children.Add(btn);
    }

    private static readonly Dictionary<string, HashSet<string>> KeyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["delete"] = new(StringComparer.OrdinalIgnoreCase) { "del" },
        ["del"] = new(StringComparer.OrdinalIgnoreCase) { "delete" },
        ["insert"] = new(StringComparer.OrdinalIgnoreCase) { "ins" },
        ["ins"] = new(StringComparer.OrdinalIgnoreCase) { "insert" },
        ["pageup"] = new(StringComparer.OrdinalIgnoreCase) { "pgup" },
        ["pgup"] = new(StringComparer.OrdinalIgnoreCase) { "pageup" },
        ["pagedown"] = new(StringComparer.OrdinalIgnoreCase) { "pgdn" },
        ["pgdn"] = new(StringComparer.OrdinalIgnoreCase) { "pagedown" },
        [KeyPrintScreen] = new(StringComparer.OrdinalIgnoreCase) { "prtsc", "prtscr" },
        ["prtsc"] = new(StringComparer.OrdinalIgnoreCase) { KeyPrintScreen, "prtscr" },
        ["prtscr"] = new(StringComparer.OrdinalIgnoreCase) { KeyPrintScreen, "prtsc" },
        ["scrolllock"] = new(StringComparer.OrdinalIgnoreCase) { "scrlk" },
        ["scrlk"] = new(StringComparer.OrdinalIgnoreCase) { "scrolllock" },
        ["numlock"] = new(StringComparer.OrdinalIgnoreCase) { "numlk" },
        ["numlk"] = new(StringComparer.OrdinalIgnoreCase) { "numlock" },
        ["ctrl"] = new(StringComparer.OrdinalIgnoreCase) { "control" },
        ["control"] = new(StringComparer.OrdinalIgnoreCase) { "ctrl" },
        ["win"] = new(StringComparer.OrdinalIgnoreCase) { "windows" },
        ["windows"] = new(StringComparer.OrdinalIgnoreCase) { "win" },
        ["esc"] = new(StringComparer.OrdinalIgnoreCase) { "escape" },
        ["escape"] = new(StringComparer.OrdinalIgnoreCase) { "esc" },
        ["numpadenter"] = new(StringComparer.OrdinalIgnoreCase) { "numenter" },
        ["numenter"] = new(StringComparer.OrdinalIgnoreCase) { "numpadenter" },
        ["add"] = new(StringComparer.OrdinalIgnoreCase) { "num+" },
        ["num+"] = new(StringComparer.OrdinalIgnoreCase) { "add" },
    };

    private static bool IsKeyMatch(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return (KeyAliases.TryGetValue(a, out var aAliases) && aAliases.Contains(b))
            || (KeyAliases.TryGetValue(b, out var bAliases) && bAliases.Contains(a));
    }

    private void UpdateVirtualKeyboardHighlights()
    {
        if (Application.Current.Resources["AccentButtonStyle"] is not Style accentStyle ||
            Application.Current.Resources["DefaultButtonStyle"] is not Style defaultStyle)
        {
            return;
        }

        var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ViewModel != null && ViewModel.ActiveStageIndex >= 0 && ViewModel.ActiveStageIndex < ViewModel.Stages.Count)
        {
            var stage = ViewModel.Stages[ViewModel.ActiveStageIndex];
            if (!string.IsNullOrWhiteSpace(stage.KeyCombo))
            {
                foreach (var key in stage.KeyCombo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    activeKeys.Add(key);
                }
            }
        }

        foreach (var (keyId, list) in _keyboardButtons)
        {
            var targetStyle = activeKeys.Contains(keyId) ? accentStyle : defaultStyle;
            foreach (var b in list)
            {
                b.Style = targetStyle;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Step Cards Rendering
    // ══════════════════════════════════════════════════════════════════════════════

    private void RenderHolderStepCards()
    {
        if (HolderStepsContainer == null || ViewModel == null) return;
        HolderStepsContainer.Children.Clear();

        if (ViewModel.ActiveStageIndex >= ViewModel.Stages.Count)
        {
            ViewModel.ActiveStageIndex = Math.Max(0, ViewModel.Stages.Count - 1);
        }

        UpdateActiveStepHint();

        for (int i = 0; i < ViewModel.Stages.Count; i++)
        {
            var cardBorder = CreateHolderStepCard(i);
            HolderStepsContainer.Children.Add(cardBorder);
        }

        UpdateHolderRepeatPanelVisibility();
    }

    private void UpdateActiveStepHint()
    {
        if (ActiveStepTargetHint == null) return;

        if (ViewModel != null && ViewModel.Stages.Count > 0 &&
            ViewModel.ActiveStageIndex >= 0 && ViewModel.ActiveStageIndex < ViewModel.Stages.Count)
        {
            string combo = ViewModel.Stages[ViewModel.ActiveStageIndex].KeyCombo;
            string comboDisplay = string.IsNullOrWhiteSpace(combo) ? LocalizationManager.Get("KeyHolder_NoKeysSet") : combo;
            ActiveStepTargetHint.Text = string.Format(LocalizationManager.Get("KeyHolder_EditingStep"), ViewModel.ActiveStageIndex + 1, comboDisplay);
        }
        else
        {
            ActiveStepTargetHint.Text = "";
        }
    }

    private Border CreateHolderStepCard(int stepIndex)
    {
        if (ViewModel == null || stepIndex < 0 || stepIndex >= ViewModel.Stages.Count)
        {
            return new Border();
        }

        var stage = ViewModel.Stages[stepIndex];
        bool isActive = (stepIndex == ViewModel.ActiveStageIndex);
        bool isRunning = (stepIndex == ViewModel.RunningStageIndex);

        string cardBackgroundKey = GetStepCardBackgroundKey(isRunning, isActive);
        var cardBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            Background = (Brush)Application.Current.Resources[cardBackgroundKey],
            BorderBrush = (Brush)Application.Current.Resources[
                isRunning || isActive ? AccentFillBrush : "CardStrokeColorDefaultBrush"
            ],
            BorderThickness = new Thickness(isActive || isRunning ? 2 : 1)
        };

        cardBorder.PointerPressed += (s, e) =>
        {
            if (!ReferenceEquals(e.OriginalSource, cardBorder))
            {
                return;
            }

            if (ViewModel.ActiveStageIndex != stepIndex)
            {
                ViewModel.ActiveStageIndex = stepIndex;
                RenderHolderStepCards();
                UpdateVirtualKeyboardHighlights();
                UpdateHolderActionBtnState();
            }
        };

        var grid = CreateStepCardGrid();
        grid.Children.Add(CreateStepBadgePanel(stepIndex, isRunning));
        grid.Children.Add(CreateKeyPillBorder(stage, isActive));
        grid.Children.Add(CreateDurationPanel(stage));
        grid.Children.Add(CreateRestPanel(stage));
        grid.Children.Add(CreateActionsPanel(stepIndex));

        cardBorder.Child = grid;
        return cardBorder;
    }

    private static string GetStepCardBackgroundKey(bool isRunning, bool isActive)
    {
        if (isRunning)
        {
            return "AccentFillColorTertiaryBrush";
        }

        if (isActive)
        {
            return "CardBackgroundFillColorSecondaryBrush";
        }

        return "CardBackgroundFillColorDefaultBrush";
    }

    private static Grid CreateStepCardGrid()
    {
        var grid = new Grid
        {
            ColumnSpacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        return grid;
    }

    private static StackPanel CreateStepBadgePanel(int stepIndex, bool isRunning)
    {
        var stepBadgePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 64
        };
        if (isRunning)
        {
            stepBadgePanel.Children.Add(new FontIcon
            {
                Glyph = "\uE768",
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources[AccentFillBrush]
            });
        }
        stepBadgePanel.Children.Add(new TextBlock
        {
            Text = string.Format(LocalizationManager.Get("KeyHolder_Step"), stepIndex + 1),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = isRunning ? (Brush)Application.Current.Resources[AccentFillBrush] :
                         (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
        });
        Grid.SetColumn(stepBadgePanel, 0);
        return stepBadgePanel;
    }

    private static Border CreateKeyPillBorder(KeyHolderStage stage, bool isActive)
    {
        var keyPillBorder = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources[
                isActive ? AccentFillBrush : "CardStrokeColorDefaultBrush"
            ],
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Width = 110
        };
        var keyPillText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(stage.KeyCombo) ? LocalizationManager.Get("KeyHolder_TapKeysBelow") : stage.KeyCombo,
            FontFamily = new FontFamily("Cascadia Code, Consolas"),
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = string.IsNullOrWhiteSpace(stage.KeyCombo) ?
                (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"] :
                (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
        };
        keyPillBorder.Child = keyPillText;
        Grid.SetColumn(keyPillBorder, 1);
        return keyPillBorder;
    }

    private StackPanel CreateDurationPanel(KeyHolderStage stage)
    {
        var durationPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        var modeCombo = new ComboBox
        {
            FontSize = 11,
            Padding = new Thickness(6, 2, 6, 2),
            Width = 140
        };
        modeCombo.Items.Add(new ComboBoxItem { Content = LocalizationManager.Get("KeyHolder_HoldUntilStopped"), FontSize = 11 });
        modeCombo.Items.Add(new ComboBoxItem { Content = LocalizationManager.Get("KeyHolder_HoldForSeconds"), FontSize = 11 });
        modeCombo.SelectedIndex = stage.HoldDurationSec > 0 ? 1 : 0;

        var holdBox = new NumberBox
        {
            Value = stage.HoldDurationSec > 0 ? stage.HoldDurationSec : 3.0,
            Minimum = 0.1,
            SmallChange = 0.5,
            FontSize = 11,
            Width = 72,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Visibility = stage.HoldDurationSec > 0 ? Visibility.Visible : Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center
        };

        modeCombo.SelectionChanged += (_, _) =>
        {
            if (modeCombo.SelectedIndex == 1)
            {
                double val = holdBox.Value;
                stage.HoldDurationSec = (!double.IsNaN(val) && !double.IsInfinity(val) && val > 0) ? Math.Max(0.1, val) : 3.0;
                holdBox.Value = stage.HoldDurationSec;
                holdBox.Visibility = Visibility.Visible;
            }
            else
            {
                stage.HoldDurationSec = 0;
                holdBox.Visibility = Visibility.Collapsed;
            }
            UpdateHolderRepeatPanelVisibility();
            UpdateHolderActionBtnState();
        };

        holdBox.ValueChanged += (_, _) =>
        {
            if (modeCombo.SelectedIndex == 1 && !double.IsNaN(holdBox.Value))
            {
                stage.HoldDurationSec = Math.Max(0.1, holdBox.Value);
            }
        };

        durationPanel.Children.Add(modeCombo);
        durationPanel.Children.Add(holdBox);
        Grid.SetColumn(durationPanel, 2);
        return durationPanel;
    }

    private StackPanel CreateRestPanel(KeyHolderStage stage)
    {
        var restPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };
        var restCheck = new CheckBox
        {
            Content = LocalizationManager.Get("KeyHolder_RestSeconds"),
            FontSize = 11,
            IsChecked = stage.RestDurationSec > 0,
            VerticalAlignment = VerticalAlignment.Center
        };
        var restBox = new NumberBox
        {
            Value = stage.RestDurationSec > 0 ? stage.RestDurationSec : 0.5,
            Minimum = 0,
            SmallChange = 0.5,
            FontSize = 11,
            Width = 66,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Visibility = stage.RestDurationSec > 0 ? Visibility.Visible : Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center
        };

        restCheck.Checked += (_, _) =>
        {
            stage.RestDurationSec = Math.Max(0.1, double.IsNaN(restBox.Value) ? 0.5 : restBox.Value);
            restBox.Visibility = Visibility.Visible;
            UpdateHolderRepeatPanelVisibility();
        };
        restCheck.Unchecked += (_, _) =>
        {
            stage.RestDurationSec = 0;
            restBox.Visibility = Visibility.Collapsed;
            UpdateHolderRepeatPanelVisibility();
        };
        restBox.ValueChanged += (_, _) =>
        {
            if (restCheck.IsChecked == true && !double.IsNaN(restBox.Value))
            {
                stage.RestDurationSec = Math.Max(0, restBox.Value);
            }
        };

        restPanel.Children.Add(restCheck);
        restPanel.Children.Add(restBox);
        Grid.SetColumn(restPanel, 3);
        return restPanel;
    }

    private StackPanel CreateActionsPanel(int stepIndex)
    {
        var actionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };

        var upBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE70E", FontSize = 10 },
            Style = Application.Current.Resources["SubtleButtonStyle"] as Style,
            Padding = new Thickness(5, 3, 5, 3),
            IsEnabled = (stepIndex > 0)
        };
        ToolTipService.SetToolTip(upBtn, LocalizationManager.Get("KeyHolder_MoveUp"));
        upBtn.Click += (_, _) =>
        {
            if (ViewModel is null) return;
            ViewModel.MoveStage(stepIndex, stepIndex - 1);
            RenderHolderStepCards();
            UpdateVirtualKeyboardHighlights();
        };

        var downBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE70D", FontSize = 10 },
            Style = Application.Current.Resources["SubtleButtonStyle"] as Style,
            Padding = new Thickness(5, 3, 5, 3),
            IsEnabled = (ViewModel != null && stepIndex < ViewModel.Stages.Count - 1)
        };
        ToolTipService.SetToolTip(downBtn, LocalizationManager.Get("KeyHolder_MoveDown"));
        downBtn.Click += (_, _) =>
        {
            if (ViewModel is null) return;
            ViewModel.MoveStage(stepIndex, stepIndex + 1);
            RenderHolderStepCards();
            UpdateVirtualKeyboardHighlights();
        };

        var delBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE711", FontSize = 10 },
            Style = Application.Current.Resources["SubtleButtonStyle"] as Style,
            Padding = new Thickness(5, 3, 5, 3),
            IsEnabled = (ViewModel != null && ViewModel.Stages.Count > 1)
        };
        ToolTipService.SetToolTip(delBtn, LocalizationManager.Get("KeyHolder_DeleteStep"));
        delBtn.Click += (_, _) =>
        {
            if (ViewModel is null || ViewModel.Stages.Count <= 1) return;
            ViewModel.RemoveStage(stepIndex);
            RenderHolderStepCards();
            UpdateVirtualKeyboardHighlights();
            UpdateHolderActionBtnState();
            UpdateHolderRepeatPanelVisibility();
        };

        actionsPanel.Children.Add(upBtn);
        actionsPanel.Children.Add(downBtn);
        actionsPanel.Children.Add(delBtn);
        Grid.SetColumn(actionsPanel, 5);
        return actionsPanel;
    }

    private void ApplyLocalization()
    {
        HolderHeaderTitle.Text = LocalizationManager.Get("KeyHolder_Title");
        HolderHeaderDesc.Text = LocalizationManager.Get("KeyHolder_Desc");
        HolderPipelineTitle.Text = LocalizationManager.Get("KeyHolder_PipelineTitle");
        HolderPipelineHint.Text = LocalizationManager.Get("KeyHolder_PipelineHint");
        HolderRepeatLabel.Text = LocalizationManager.Get("KeyHolder_RepeatTitle");
        AddStepBtnText.Text = LocalizationManager.Get("KeyHolder_AddStepBtn");
        HolderVkTitle.Text = LocalizationManager.Get("KeyHolder_VkTitle");
        CaptureKeyText.Text = LocalizationManager.Get("KeyHolder_CaptureBtn");
        ClearActiveStepKeysBtnText.Text = LocalizationManager.Get("KeyHolder_ClearKeysBtn");
        HolderDelayLabel.Text = LocalizationManager.Get("Common_Delay");

        if (ViewModel is { IsRunning: false })
        {
            HolderActionText.Text = LocalizationManager.Get("KeyHolder_ActionStart");
            if (string.IsNullOrWhiteSpace(HolderStatusText.Text) ||
                HolderStatusText.Text == "Ready" || HolderStatusText.Text == "就緒" ||
                HolderStatusText.Text == "就绪" || HolderStatusText.Text == "待機中")
            {
                HolderStatusText.Text = LocalizationManager.Get("Common_Ready");
            }
        }

        UpdateActiveStepHint();
        RenderHolderStepCards();
    }
}
