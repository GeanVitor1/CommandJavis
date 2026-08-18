using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JarvisComando;

public partial class AppIconControl : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(ImageSource), typeof(AppIconControl), new PropertyMetadata(null));

    public static readonly DependencyProperty HasIconProperty =
        DependencyProperty.Register(nameof(HasIcon), typeof(bool), typeof(AppIconControl), new PropertyMetadata(false));

    public static readonly DependencyProperty AvatarCharProperty =
        DependencyProperty.Register(nameof(AvatarChar), typeof(string), typeof(AppIconControl), new PropertyMetadata("?"));

    public static readonly DependencyProperty CategoryProperty =
        DependencyProperty.Register(nameof(Category), typeof(string), typeof(AppIconControl), new PropertyMetadata("app"));

    public static readonly DependencyProperty SizeProperty =
        DependencyProperty.Register(nameof(Size), typeof(double), typeof(AppIconControl), new PropertyMetadata(36.0, OnSizeChanged));

    public ImageSource? Icon
    {
        get => (ImageSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool HasIcon
    {
        get => (bool)GetValue(HasIconProperty);
        set => SetValue(HasIconProperty, value);
    }

    public string AvatarChar
    {
        get => (string)GetValue(AvatarCharProperty);
        set => SetValue(AvatarCharProperty, value);
    }

    public string Category
    {
        get => (string)GetValue(CategoryProperty);
        set => SetValue(CategoryProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public AppIconControl()
    {
        InitializeComponent();
        ApplySize(Size);
    }

    private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((AppIconControl)d).ApplySize((double)e.NewValue);

    private void ApplySize(double size)
    {
        if (Circle == null) return;
        var radius = new CornerRadius(size / 2);
        Circle.CornerRadius = radius;
        GradientLayer.CornerRadius = radius;
        FallbackLayer.CornerRadius = radius;
        IconImg.Width = size * 0.6;
        IconImg.Height = size * 0.6;
        Letter.FontSize = Math.Max(9, size * 0.4);
    }
}