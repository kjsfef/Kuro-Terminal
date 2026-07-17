using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace Kuro;

public partial class KuroEditorWindow : Window
{
    private string _path;
    private bool _dirty;
    private bool _loading;

    public KuroEditorWindow(string path)
    {
        InitializeComponent();
        _path = Path.GetFullPath(path);
        LoadDocument();
    }

    private void LoadDocument()
    {
        _loading = true;
        try
        {
            EditorBox.Text = File.Exists(_path)
                ? File.ReadAllText(_path)
                : string.Empty;

            _dirty = false;
            RefreshTitle();
            RefreshStatus();
        }
        finally
        {
            _loading = false;
        }
    }

    private void RefreshTitle()
    {
        string marker = _dirty ? " •" : string.Empty;
        EditorTitle.Text = "Kuro Editor" + marker;
        EditorPath.Text = _path;
        Title = $"{Path.GetFileName(_path)}{marker} — Kuro Editor";
    }

    private void RefreshStatus()
    {
        int line = EditorBox.GetLineIndexFromCharacterIndex(EditorBox.CaretIndex) + 1;
        int column = EditorBox.CaretIndex -
                     EditorBox.GetCharacterIndexFromLineIndex(Math.Max(0, line - 1)) + 1;
        int lineCount = Math.Max(1, EditorBox.LineCount);

        StatusText.Text =
            $"Ln {line}, Col {column}  •  {lineCount:n0} lines  •  " +
            $"{EditorBox.Text.Length:n0} chars  •  UTF-8";
    }

    private bool SaveDocument()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_path, EditorBox.Text, new UTF8Encoding(false));
            _dirty = false;
            RefreshTitle();
            RefreshStatus();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Kuro Editor could not save",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }
    }

    private bool SaveDocumentAs()
    {
        SaveFileDialog dialog = new()
        {
            Title = "Save with Kuro Editor",
            FileName = Path.GetFileName(_path),
            InitialDirectory = Path.GetDirectoryName(_path),
            AddExtension = false,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
            return false;

        _path = dialog.FileName;
        return SaveDocument();
    }

    private bool ConfirmClose()
    {
        if (!_dirty)
            return true;

        MessageBoxResult answer = MessageBox.Show(
            this,
            $"Save changes to {Path.GetFileName(_path)}?",
            "Kuro Editor",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        return answer switch
        {
            MessageBoxResult.Yes => SaveDocument(),
            MessageBoxResult.No => true,
            _ => false
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        EditorBox.Focus();
        EditorBox.CaretIndex = EditorBox.Text.Length;
        RefreshStatus();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!ConfirmClose())
            e.Cancel = true;
    }

    private void EditorBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_loading)
        {
            _dirty = true;
            RefreshTitle();
        }

        RefreshStatus();
    }

    private void EditorBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            SaveDocument();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) &&
                 e.Key == Key.S)
        {
            SaveDocumentAs();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.W)
        {
            Close();
            e.Handled = true;
        }
        else
        {
            Dispatcher.BeginInvoke(new Action(RefreshStatus));
        }
    }

    private void WrapCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        EditorBox.TextWrapping = WrapCheckBox.IsChecked == true
            ? TextWrapping.Wrap
            : TextWrapping.NoWrap;

        EditorBox.HorizontalScrollBarVisibility = WrapCheckBox.IsChecked == true
            ? System.Windows.Controls.ScrollBarVisibility.Disabled
            : System.Windows.Controls.ScrollBarVisibility.Auto;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => SaveDocument();

    private void SaveAs_Click(object sender, RoutedEventArgs e) => SaveDocumentAs();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Minimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Maximize_Click(sender, e);
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void OpenCode_Click(object sender, RoutedEventArgs e)
    {
        string? code = FindCodeExecutable();
        if (code is null)
        {
            MessageBox.Show(
                this,
                "Visual Studio Code was not found. Install it or make sure the 'code' command is in PATH.",
                "Kuro Editor",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = code,
            Arguments = Quote(_path),
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(_path) ?? Environment.CurrentDirectory
        });
    }

    private static string? FindCodeExecutable()
    {
        string[] candidates =
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Microsoft VS Code", "Code.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft VS Code", "Code.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft VS Code", "Code.exe")
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory.Trim('"'), "code.cmd");
            if (File.Exists(candidate))
                return candidate;

            candidate = Path.Combine(directory.Trim('"'), "code.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string Quote(string value) =>
        "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
