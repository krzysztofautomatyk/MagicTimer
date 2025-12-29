using System.Windows;
using System.Windows.Media;

namespace MagicTimer;

public partial class FontPickerDialog : Window
{
    public string? SelectedFontFamily { get; private set; }

    public FontPickerDialog(Window owner, string currentFont)
    {
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Title = "Wybierz czcionkê";
        Width = 400;
        Height = 500;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B0E14"));

        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(16) };

        var listBox = new System.Windows.Controls.ListBox
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827")),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6EDF3")),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#223344")),
            FontSize = 14,
            Height = 380
        };

        foreach (var font in Fonts.SystemFontFamilies.OrderBy(f => f.Source))
        {
            var item = new System.Windows.Controls.ListBoxItem
            {
                Content = font.Source,
                FontFamily = font,
                FontSize = 16
            };

            if (font.Source.Equals(currentFont, StringComparison.OrdinalIgnoreCase))
                item.IsSelected = true;

            listBox.Items.Add(item);
        }

        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var okButton = new System.Windows.Controls.Button
        {
            Content = "OK",
            Width = 80,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2EA043")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold
        };

        okButton.Click += (_, _) =>
        {
            if (listBox.SelectedItem is System.Windows.Controls.ListBoxItem selected)
            {
                SelectedFontFamily = selected.Content?.ToString();
                DialogResult = true;
                Close();
            }
        };

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "Anuluj",
            Width = 80,
            Height = 32,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#30363D")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold
        };

        cancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        var stackPanel = new System.Windows.Controls.StackPanel();
        stackPanel.Children.Add(listBox);
        stackPanel.Children.Add(buttonPanel);

        grid.Children.Add(stackPanel);
        Content = grid;

        if (listBox.SelectedItem != null)
            listBox.ScrollIntoView(listBox.SelectedItem);
    }
}
