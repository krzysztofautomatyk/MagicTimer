using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MagicTimer.Settings;

namespace MagicTimer;

public partial class ColorPickerDialog : Window
{
    private readonly AppSettings _settings;
    private readonly Action _onColorsChanged;

    private readonly Dictionary<string, TextBox> _colorTextBoxes = new();
    private readonly Dictionary<string, Rectangle> _colorPreviews = new();

    public ColorPickerDialog(Window owner, AppSettings settings, Action onColorsChanged)
    {
        Owner = owner;
        _settings = settings;
        _onColorsChanged = onColorsChanged;

        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Title = "Ustawienia kolorów";
        Width = 450;
        Height = 520;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B0E14"));

        BuildUI();
    }

    private void BuildUI()
    {
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(16)
        };

        var mainStack = new StackPanel();

        var colorItems = new (string Label, string PropertyName, Func<string> Getter, Action<string> Setter)[]
        {
            ("T³o aplikacji", "BackgroundColor", () => _settings.BackgroundColor, v => _settings.BackgroundColor = v),
            ("T³o zegarka", "TimerBackgroundColor", () => _settings.TimerBackgroundColor, v => _settings.TimerBackgroundColor = v),
            ("Kolor czasu", "TimerForegroundColor", () => _settings.TimerForegroundColor, v => _settings.TimerForegroundColor = v),
            ("Pasek postêpu", "ProgressBarColor", () => _settings.ProgressBarColor, v => _settings.ProgressBarColor = v),
            ("Przycisk Start", "StartButtonColor", () => _settings.StartButtonColor, v => _settings.StartButtonColor = v),
            ("Przycisk Stop", "StopButtonColor", () => _settings.StopButtonColor, v => _settings.StopButtonColor = v),
            ("Kolor mrugania (alarm)", "BlinkColor", () => _settings.BlinkColor, v => _settings.BlinkColor = v),
            ("T³o przycisków", "ButtonBackgroundColor", () => _settings.ButtonBackgroundColor, v => _settings.ButtonBackgroundColor = v),
            ("Kolor tekstu/labeli", "TextColor", () => _settings.TextColor, v => _settings.TextColor = v),
            ("T³o pola czasu", "InputBackgroundColor", () => _settings.InputBackgroundColor, v => _settings.InputBackgroundColor = v),
            ("T³o banera przypomnienia", "BannerBackgroundColor", () => _settings.BannerBackgroundColor, v => _settings.BannerBackgroundColor = v),
        };

        foreach (var item in colorItems)
        {
            var row = CreateColorRow(item.Label, item.PropertyName, item.Getter(), item.Setter);
            mainStack.Children.Add(row);
        }

        // Przyciski
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var resetButton = new Button
        {
            Content = "Resetuj",
            Width = 90,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D73A49")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold
        };
        resetButton.Click += ResetButton_Click;

        var okButton = new Button
        {
            Content = "Zapisz",
            Width = 90,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2EA043")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold
        };
        okButton.Click += OkButton_Click;

        var cancelButton = new Button
        {
            Content = "Anuluj",
            Width = 90,
            Height = 32,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#30363D")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold
        };
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };

        buttonPanel.Children.Add(resetButton);
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        mainStack.Children.Add(buttonPanel);

        scrollViewer.Content = mainStack;
        Content = scrollViewer;
    }

    private UIElement CreateColorRow(string label, string propertyName, string currentValue, Action<string> setter)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6EDF3")),
            Width = 160,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12
        };

        var preview = new Rectangle
        {
            Width = 32,
            Height = 24,
            Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444")),
            StrokeThickness = 1,
            Margin = new Thickness(0, 0, 8, 0)
        };
        TrySetRectangleFill(preview, currentValue);
        _colorPreviews[propertyName] = preview;

        var textBox = new TextBox
        {
            Text = currentValue,
            Width = 100,
            Height = 24,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827")),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6EDF3")),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#223344")),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 12
        };
        textBox.TextChanged += (_, _) =>
        {
            TrySetRectangleFill(preview, textBox.Text);
        };
        textBox.Tag = setter;
        _colorTextBoxes[propertyName] = textBox;

        var pickerButton = new Button
        {
            Content = "...",
            Width = 32,
            Height = 24,
            Margin = new Thickness(8, 0, 0, 0),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#30363D")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        pickerButton.Click += (_, _) =>
        {
            var colorDialog = new System.Windows.Forms.ColorDialog();
            if (TryParseColor(textBox.Text, out var existingColor))
            {
                colorDialog.Color = System.Drawing.Color.FromArgb(existingColor.R, existingColor.G, existingColor.B);
            }

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var c = colorDialog.Color;
                var hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                textBox.Text = hex;
            }
        };

        row.Children.Add(labelBlock);
        row.Children.Add(preview);
        row.Children.Add(textBox);
        row.Children.Add(pickerButton);

        return row;
    }

    private void TrySetRectangleFill(Rectangle rect, string colorText)
    {
        if (TryParseColor(colorText, out var color))
        {
            rect.Fill = new SolidColorBrush(color);
        }
    }

    private static bool TryParseColor(string text, out Color color)
    {
        color = default;
        try
        {
            color = (Color)ColorConverter.ConvertFromString(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Zapisz wszystkie wartoœci
        foreach (var kvp in _colorTextBoxes)
        {
            if (kvp.Value.Tag is Action<string> setter)
            {
                setter(kvp.Value.Text);
            }
        }

        _onColorsChanged();
        DialogResult = true;
        Close();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var defaults = new Dictionary<string, string>
        {
            ["BackgroundColor"] = "#0B0E14",
            ["TimerBackgroundColor"] = "#0F172A",
            ["TimerForegroundColor"] = "#E6EDF3",
            ["ProgressBarColor"] = "#58A6FF",
            ["StartButtonColor"] = "#2EA043",
            ["StopButtonColor"] = "#D73A49",
            ["BlinkColor"] = "#7F1D1D",
            ["ButtonBackgroundColor"] = "#30363D",
            ["TextColor"] = "#E6EDF3",
            ["InputBackgroundColor"] = "#111827",
            ["BannerBackgroundColor"] = "#7F1D1D"
        };

        foreach (var kvp in defaults)
        {
            if (_colorTextBoxes.TryGetValue(kvp.Key, out var textBox))
            {
                textBox.Text = kvp.Value;
            }
        }
    }
}
