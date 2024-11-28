using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace SharpVectors.Converters;

/// <summary>
///     Interaction logic for OptionsPage.xaml
/// </summary>
public partial class OptionsPage : Page
{
    private bool _isInitialising;

    public OptionsPage()
    {
        InitializeComponent();

        // Reset the dimensions...
        Width = double.NaN;
        Height = double.NaN;

        Loaded += OnOptionsPageLoaded;
    }

    public ConverterOptions Options { get; set; }

    private void OnOptionsPageLoaded(object sender, RoutedEventArgs e)
    {
        Debug.Assert(Options != null);
        if (Options == null) Options = new ConverterOptions();

        _isInitialising = true;

        chkTextAsGeometry.IsChecked = Options.TextAsGeometry;
        chkIncludeRuntime.IsChecked = Options.IncludeRuntime;

        chkXaml.IsChecked = Options.GeneralWpf;
        panelXaml.IsEnabled = Options.GeneralWpf;
        chkSameXaml.IsChecked = Options.SaveXaml;
        chkSameZaml.IsChecked = Options.SaveZaml;
        chkXamlWriter.IsChecked = Options.UseCustomXamlWriter;

        chkImage.IsChecked = Options.GenerateImage;
        panelImage.IsEnabled = Options.GenerateImage;
        cboImages.SelectedIndex = (int)Options.EncoderType;

        _isInitialising = false;
    }

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitialising) return;

        _isInitialising = true;

        if (chkImage == sender)
            if (panelImage != null)
                panelImage.IsEnabled = chkImage.IsChecked.Value;

        if (chkXaml == sender)
            if (panelXaml != null)
                panelXaml.IsEnabled = chkXaml.IsChecked.Value;

        Options.TextAsGeometry = chkTextAsGeometry.IsChecked.Value;
        Options.IncludeRuntime = chkIncludeRuntime.IsChecked.Value;

        Options.GeneralWpf = chkXaml.IsChecked.Value;
        //_options.GeneralWpf = panelXaml.IsEnabled;
        Options.SaveXaml = chkSameXaml.IsChecked.Value;
        Options.SaveZaml = chkSameZaml.IsChecked.Value;
        Options.UseCustomXamlWriter = chkXamlWriter.IsChecked.Value;

        Options.GenerateImage = chkImage.IsChecked.Value;
        //_options.GenerateImage = panelImage.IsEnabled;
        if (cboImages.SelectedIndex >= 0) Options.EncoderType = (ImageEncoderType)cboImages.SelectedIndex;

        _isInitialising = false;
    }
}