using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CG34;

/// <summary>
/// Logika interakcji dla klasy ColorPicker.xaml
/// </summary>
public partial class ColorPicker : UserControl
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color),
        typeof(Color),
        typeof(ColorPicker));

    public MainWindow MainWindow { get; set; } = default!;

    public event EventHandler ColorChanged;

    protected virtual void OnColorChanged()
    {
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);

        set => SetValue(ColorProperty, value);
    }

    public ColorPicker()
    {
        InitializeComponent();
    }

    private async void ColorSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider slider)
            return;
        var color = Color.FromArgb(Color.A, Color.R, Color.G, Color.B);
        switch (slider.Tag)
        {
            case "R":
                color.R = (byte)e.NewValue;
                break;
            case "G":
                color.G = (byte)e.NewValue;
                break;
            case "B":
                color.B = (byte)e.NewValue;
                break;
            case "A":
                color.A = (byte)e.NewValue;
                break;
            default:
                break;
        }

        Color = color;
        OnColorChanged();
    }
}