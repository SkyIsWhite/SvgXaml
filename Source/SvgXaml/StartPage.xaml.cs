using System.Windows;
using System.Windows.Controls;

namespace SharpVectors.Converters;

/// <summary>
///     Interaction logic for StartPage.xaml
/// </summary>
public partial class StartPage : Page
{
    public StartPage()
    {
        InitializeComponent();

        // Reset the dimensions...
        Width = double.NaN;
        Height = double.NaN;

        Loaded += OnStartPageLoaded;
    }

    private void OnStartPageLoaded(object sender, RoutedEventArgs e)
    {
    }
}