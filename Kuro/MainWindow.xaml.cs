using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Kuro;

public partial class MainWindow : Window
{
    private Brush _blue = Brushes.DeepSkyBlue;
    private Brush _cyan = Brushes.Cyan;
    private Brush _green = Brushes.LightGreen;
    private Brush _yellow = Brushes.Khaki;
    private Brush _red = Brushes.IndianRed;
    private Brush _purple = Brushes.Plum;
    private Brush _text = Brushes.WhiteSmoke;
    private Brush _muted = Brushes.Gray;

    private readonly ShellEngine _shell = new();
    private readonly List<string> _history = new();

    private Paragraph? _inputParagraph;
    private Run? _inputAnchor;
    private int _historyIndex;
    private string _historyDraft = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        TitleVersionText.Text = "v" + ShellEngine.Version;

        Loaded += (_, _) =>
        {
            ApplyAppearance();
            if (_shell.Config.ShowGreeting)
                PrintWelcome();
            BeginPrompt();
            TerminalBox.Focus();
            if (_shell.Config.AutoUpdate)
                _ = CheckForUpdatesOnStartupAsync();
        };
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            await AutoUpdater.CheckAndApplyAsync();
        }
        catch
        {
            // Startup updates stay silent so the shell never exposes owner release controls.
        }
    }

    private void ApplyAppearance()
    {
        ThemePalette palette = ThemePalette.Get(_shell.Config.Theme);
        _blue = BrushFromHex(palette.Accent);
        _cyan = BrushFromHex(palette.Cyan);
        _green = BrushFromHex(palette.Green);
        _yellow = BrushFromHex(palette.Yellow);
        _red = BrushFromHex(palette.Red);
        _purple = BrushFromHex(palette.Purple);
        _text = BrushFromHex(palette.Text);
        _muted = BrushFromHex(palette.Muted);

        WindowFrame.Background = BrushWithOpacity(palette.Background, _shell.Config.Opacity);
        WindowFrame.BorderBrush = BrushWithOpacity(palette.Border, 0.26);
        OuterFrame.BorderBrush = GradientBrush(palette.Cyan, palette.Purple, 0.42, 0.34);
        InnerHighlight.BorderBrush = BrushWithOpacity(palette.Text, 0.055);
        TitleBar.Background = BrushWithOpacity(palette.TitleBackground, Math.Min(0.90, _shell.Config.Opacity + 0.055));
        TitleBar.BorderBrush = BrushWithOpacity(palette.Border, 0.16);
        TerminalBox.Background = BrushWithOpacity(palette.TerminalBackground, Math.Max(0.10, _shell.Config.Opacity - 0.46));
        TerminalBox.Foreground = _text;
        TerminalBox.CaretBrush = _cyan;
        TerminalBox.SelectionBrush = BrushWithOpacity(palette.Accent, 0.34);
        TerminalBox.FontSize = _shell.Config.FontSize;
        TerminalBox.Document.FontSize = _shell.Config.FontSize;
        TerminalBox.Document.Foreground = _text;

        TopGlow.Background = GradientBrush(palette.Cyan, palette.Purple, 0.72, 0.58);
        BottomGlow.Background = GradientBrush(palette.Cyan, palette.Purple, 0.50, 0.42);
        AccentRail.Background = BrushWithOpacity(palette.Cyan, 0.30);
        Foreground = _text;
        TitleNameText.Foreground = _text;
        TitlePathText.Foreground = _muted;
        RefreshWindowTitle();
    }

    private void PrintWelcome()
    {
        Paragraph heading = NewParagraph();
        heading.Inlines.Add(new Run("â—‰")
        {
            Foreground = _purple,
            FontWeight = FontWeights.Bold,
            FontSize = _shell.Config.FontSize + 5
        });
        heading.Inlines.Add(new Run("  K U R O")
        {
            Foreground = _blue,
            FontWeight = FontWeights.Bold
        });
        heading.Inlines.Add(new Run($"  v{ShellEngine.Version}") { Foreground = _purple });
        TerminalBox.Document.Blocks.Add(heading);

        Paragraph identity = NewParagraph();
        identity.Inlines.Add(new Run("custom terminal shell") { Foreground = _muted });
        identity.Inlines.Add(new Run($"  //  {_shell.CommandCount} built-ins") { Foreground = _muted });
        TerminalBox.Document.Blocks.Add(identity);

        if (!string.IsNullOrWhiteSpace(_shell.Config.Motd))
        {
            Paragraph motd = NewParagraph();
            motd.Inlines.Add(new Run("â—ˆ ") { Foreground = _green });
            motd.Inlines.Add(new Run(_shell.Config.Motd) { Foreground = _text });
            TerminalBox.Document.Blocks.Add(motd);
        }

        Paragraph hint = NewParagraph();
        hint.Inlines.Add(new Run("Try ") { Foreground = _muted });
        hint.Inlines.Add(new Run("help") { Foreground = _yellow, FontWeight = FontWeights.SemiBold });
        hint.Inlines.Add(new Run(", ") { Foreground = _muted });
        hint.Inlines.Add(new Run("security") { Foreground = _green });
        hint.Inlines.Add(new Run(", ") { Foreground = _muted });
        hint.Inlines.Add(new Run("theme random") { Foreground = _purple });
        hint.Inlines.Add(new Run(", or press ") { Foreground = _muted });
        hint.Inlines.Add(new Run("Tab") { Foreground = _cyan });
        hint.Inlines.Add(new Run(" to complete.") { Foreground = _muted });
        TerminalBox.Document.Blocks.Add(hint);

        TerminalBox.Document.Blocks.Add(NewParagraph());
    }

    private void BeginPrompt()
    {
        Paragraph paragraph = NewParagraph();
        AppendPrompt(paragraph, _shell.Config.PromptFormat);

        // Keep a permanent zero-width marker directly after the prompt.
        // A stored numeric document offset shifts when the first character is typed,
        // which caused commands such as "help" to be read as "elp".
        Run inputAnchor = new("\u2060")
        {
            Foreground = _text
        };
        paragraph.Inlines.Add(inputAnchor);

        TerminalBox.Document.Blocks.Add(paragraph);
        _inputParagraph = paragraph;
        _inputAnchor = inputAnchor;
        _historyIndex = _history.Count;
        _historyDraft = string.Empty;

        TitlePathText.Text = _shell.DisplayPath;
        MoveCaretToInputEnd();
        TerminalBox.ScrollToEnd();
    }

    private void AppendPrompt(Paragraph paragraph, string format)
    {
        Regex tokenPattern = new(@"\{(user|host|machine|path|fullpath|symbol)\}", RegexOptions.IgnoreCase);
        int position = 0;

        foreach (Match match in tokenPattern.Matches(format))
        {
            if (match.Index > position)
                paragraph.Inlines.Add(new Run(format[position..match.Index]) { Foreground = _muted });

            string token = match.Groups[1].Value.ToLowerInvariant();
            string value = token switch
            {
                "user" => _shell.UserName,
                "host" => "kuro",
                "machine" => _shell.HostName,
                "path" => _shell.DisplayPath,
                "fullpath" => _shell.CurrentDirectory,
                "symbol" => _shell.IsAdministrator ? "#" : "$",
                _ => match.Value
            };

            Brush color = token switch
            {
                "user" => _green,
                "host" or "machine" => _cyan,
                "path" or "fullpath" => _blue,
                "symbol" => _shell.IsAdministrator ? _red : _text,
                _ => _text
            };

            paragraph.Inlines.Add(new Run(value)
            {
                Foreground = color,
                FontWeight = FontWeights.SemiBold
            });
            position = match.Index + match.Length;
        }

        if (position < format.Length)
            paragraph.Inlines.Add(new Run(format[position..]) { Foreground = _muted });
    }

    private void TerminalBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_inputParagraph is null)
            return;

        bool control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        if (control && e.Key == Key.L)
        {
            e.Handled = true;
            ClearTerminal();
            return;
        }

        if (control && e.Key == Key.A)
        {
            e.Handled = true;
            MoveCaretToInputStart();
            return;
        }

        if (control && e.Key == Key.E)
        {
            e.Handled = true;
            MoveCaretToInputEnd();
            return;
        }

        if (control && e.Key == Key.U)
        {
            e.Handled = true;
            DeleteFromPromptToCaret();
            return;
        }

        if (control && e.Key == Key.C)
        {
            e.Handled = true;
            if (!TerminalBox.Selection.IsEmpty)
                Clipboard.SetText(new TextRange(TerminalBox.Selection.Start, TerminalBox.Selection.End).Text);
            else
                CancelCurrentCommand();
            return;
        }

        if (control && e.Key == Key.V)
        {
            e.Handled = true;
            PasteIntoInput();
            return;
        }

        if (e.Key == Key.Tab)
        {
            e.Handled = true;
            CompleteCurrentInput();
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ExecuteCurrentCommand();
            return;
        }

        if (e.Key == Key.Up)
        {
            e.Handled = true;
            MoveHistory(-1);
            return;
        }

        if (e.Key == Key.Down)
        {
            e.Handled = true;
            MoveHistory(1);
            return;
        }

        if (e.Key == Key.Home)
        {
            e.Handled = true;
            MoveCaretToInputStart();
            return;
        }

        if ((e.Key == Key.Back || e.Key == Key.Left) &&
            IsCaretAtOrBeforeInputStart() && TerminalBox.Selection.IsEmpty)
        {
            e.Handled = true;
            return;
        }

        if ((e.Key == Key.Back || e.Key == Key.Delete) && SelectionTouchesReadOnlyHistory())
        {
            e.Handled = true;
            MoveCaretToInputEnd();
        }
    }

    private void TerminalBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_inputParagraph is null)
            return;

        if (SelectionTouchesReadOnlyHistory() || IsCaretBeforeInputStart())
            MoveCaretToInputEnd();
    }

    private void ExecuteCurrentCommand()
    {
        string input = GetCurrentInput().Trim();

        if (!string.IsNullOrWhiteSpace(input))
        {
            _history.Add(input);
            _historyIndex = _history.Count;
        }

        ShellResult result = _shell.ExecuteHistoryCommand(input, _history);

        if (result.ClearHistory)
            _history.Clear();

        if (result.UiAction == ShellUiAction.ApplyAppearance)
            ApplyAppearance();
        else if (result.UiAction == ShellUiAction.RefreshTitle)
            RefreshWindowTitle();

        if (result.ClearScreen)
        {
            ClearTerminal();
            return;
        }

        if (!string.IsNullOrEmpty(result.Output))
            AppendOutput(result.Output, result.IsError);

        if (result.ExitRequested)
        {
            Close();
            return;
        }

        BeginPrompt();
    }

    private void CompleteCurrentInput()
    {
        string input = GetCurrentInput();
        CompletionResult result = _shell.Complete(input);

        if (!string.Equals(result.CompletedInput, input, StringComparison.Ordinal))
        {
            ReplaceCurrentInput(result.CompletedInput);
            return;
        }

        if (result.Matches.Count > 1)
        {
            AppendOutput(string.Join("  ", result.Matches), false);
            BeginPrompt();
            ReplaceCurrentInput(input);
        }
    }

    private void CancelCurrentCommand()
    {
        MoveCaretToInputEnd();
        _inputParagraph?.Inlines.Add(new Run("^C") { Foreground = _red });
        BeginPrompt();
    }

    private void ClearTerminal()
    {
        TerminalBox.Document.Blocks.Clear();
        BeginPrompt();
    }

    private void MoveHistory(int direction)
    {
        if (_history.Count == 0)
            return;

        if (_historyIndex == _history.Count && direction < 0)
            _historyDraft = GetCurrentInput();

        _historyIndex = Math.Clamp(_historyIndex + direction, 0, _history.Count);
        ReplaceCurrentInput(_historyIndex == _history.Count ? _historyDraft : _history[_historyIndex]);
    }

    private void ReplaceCurrentInput(string text)
    {
        if (_inputParagraph is null)
            return;

        TextPointer start = GetInputStart();
        new TextRange(start, _inputParagraph.ContentEnd).Text = text;
        MoveCaretToInputEnd();
    }

    private string GetCurrentInput()
    {
        if (_inputParagraph is null)
            return string.Empty;
        return new TextRange(GetInputStart(), _inputParagraph.ContentEnd).Text;
    }

    private void PasteIntoInput()
    {
        if (!Clipboard.ContainsText())
            return;

        if (SelectionTouchesReadOnlyHistory() || IsCaretBeforeInputStart())
            MoveCaretToInputEnd();

        string text = Clipboard.GetText().Replace("\r", string.Empty).Replace("\n", " ");
        TerminalBox.CaretPosition.InsertTextInRun(text);
        MoveCaretToInputEnd();
    }

    private void DeleteFromPromptToCaret()
    {
        if (_inputParagraph is null)
            return;

        TextPointer start = GetInputStart();
        TextPointer end = TerminalBox.CaretPosition.CompareTo(start) < 0
            ? _inputParagraph.ContentEnd
            : TerminalBox.CaretPosition;
        new TextRange(start, end).Text = string.Empty;
    }

    private void AppendOutput(string output, bool isError)
    {
        Paragraph paragraph = NewParagraph();
        paragraph.Inlines.Add(new Run(output) { Foreground = isError ? _red : _text });
        TerminalBox.Document.Blocks.Add(paragraph);
        TerminalBox.ScrollToEnd();
    }

    private TextPointer GetInputStart()
    {
        return _inputAnchor?.ContentStart.GetPositionAtOffset(1, LogicalDirection.Forward)
               ?? _inputParagraph?.ContentStart
               ?? TerminalBox.Document.ContentEnd;
    }

    private bool IsCaretAtOrBeforeInputStart() =>
        TerminalBox.CaretPosition.CompareTo(GetInputStart()) <= 0;

    private bool IsCaretBeforeInputStart() =>
        TerminalBox.CaretPosition.CompareTo(GetInputStart()) < 0;

    private bool SelectionTouchesReadOnlyHistory() =>
        TerminalBox.Selection.Start.CompareTo(GetInputStart()) < 0;

    private void MoveCaretToInputStart()
    {
        TextPointer start = GetInputStart();
        TerminalBox.Selection.Select(start, start);
        TerminalBox.CaretPosition = start;
    }

    private void MoveCaretToInputEnd()
    {
        if (_inputParagraph is null)
            return;

        TextPointer end = _inputParagraph.ContentEnd;
        TerminalBox.Selection.Select(end, end);
        TerminalBox.CaretPosition = end;
        TerminalBox.Focus();
        TerminalBox.ScrollToEnd();
    }

    private Paragraph NewParagraph() => new()
    {
        Margin = new Thickness(0),
        LineHeight = Math.Max(18, _shell.Config.FontSize + 7),
        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
        FontSize = _shell.Config.FontSize
    };

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeButton_OnClick(object sender, RoutedEventArgs e) =>
        ToggleMaximize();

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Window_OnStateChanged(object? sender, EventArgs e)
    {
        bool maximized = WindowState == WindowState.Maximized;
        ShadowFrame.Visibility = Visibility.Collapsed;

        OuterFrame.Margin = maximized ? new Thickness(0) : new Thickness(1);
        OuterFrame.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(22);
        WindowFrame.Margin = maximized ? new Thickness(0) : new Thickness(1);
        WindowFrame.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(21);
        InnerHighlight.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(20);
        TitleBar.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(20, 20, 0, 0);
        MaximizeGlyph.Visibility = maximized
            ? Visibility.Collapsed
            : Visibility.Visible;
        RestoreGlyph.Visibility = maximized
            ? Visibility.Visible
            : Visibility.Collapsed;
        MaximizeButton.ToolTip = maximized ? "Restore" : "Maximize";
    }

    private void RefreshWindowTitle()
    {
        string title = string.IsNullOrWhiteSpace(_shell.Config.WindowTitle)
            ? "kuro"
            : _shell.Config.WindowTitle;
        TitleNameText.Text = title;
        Title = title.Equals("kuro", StringComparison.OrdinalIgnoreCase)
            ? "Kuro Terminal"
            : $"{title} â€” Kuro Terminal";
    }

    private static Brush BrushFromHex(string hex) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

    private static Brush BrushWithOpacity(string hex, double opacity)
    {
        Color color = (Color)ColorConverter.ConvertFromString(hex);
        color.A = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }

    private static Brush GradientBrush(
        string startHex,
        string endHex,
        double startOpacity,
        double endOpacity)
    {
        Color start = (Color)ColorConverter.ConvertFromString(startHex);
        Color end = (Color)ColorConverter.ConvertFromString(endHex);
        start.A = (byte)Math.Round(Math.Clamp(startOpacity, 0, 1) * 255);
        end.A = (byte)Math.Round(Math.Clamp(endOpacity, 0, 1) * 255);

        LinearGradientBrush brush = new()
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop(start, 0));
        brush.GradientStops.Add(new GradientStop(end, 1));
        brush.Freeze();
        return brush;
    }
}
