using System.Windows;
using System.Windows.Controls;

namespace Vox;

public partial class WatermarkTextBox : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(WatermarkTextBox), new PropertyMetadata(null));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(WatermarkTextBox),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public string? Placeholder
    {
        get => (string?)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public event TextChangedEventHandler TextChanged
    {
        add => Box.TextChanged += value;
        remove => Box.TextChanged -= value;
    }

    public static readonly DependencyProperty HasErrorProperty =
        DependencyProperty.Register(nameof(HasError), typeof(bool), typeof(WatermarkTextBox), new PropertyMetadata(false));

    public bool HasError
    {
        get => (bool)GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    public WatermarkTextBox()
    {
        InitializeComponent();
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var w = (WatermarkTextBox)d;
        if (w.Box.Text != (string?)e.NewValue)
            w.Box.Text = (string?)e.NewValue ?? "";
    }

    private void Box_TextChanged(object sender, TextChangedEventArgs e)
    {
        SetCurrentValue(TextProperty, Box.Text);
        WatermarkText.Visibility = string.IsNullOrEmpty(Box.Text) ? Visibility.Visible : Visibility.Collapsed;
    }
}