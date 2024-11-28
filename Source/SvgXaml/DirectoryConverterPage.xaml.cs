using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SharpVectors.Converters;

/// <summary>
///     Interaction logic for DirectoryConverterPage.xaml
/// </summary>
public partial class DirectoryConverterPage : Page, IObservable, IObserver
{
    #region Constructors and Destructor

    public DirectoryConverterPage()
    {
        InitializeComponent();

        // Reset the dimensions...
        Width = double.NaN;
        Height = double.NaN;

        if (_titleBkDefault == null &&
            statusTitle != null && statusTitle.IsInitialized)
            _titleBkDefault = statusTitle.Background;

        Loaded += OnPageLoaded;
    }

    #endregion Constructors and Destructor

    #region Protected Methods

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        if (_titleBkDefault == null) _titleBkDefault = statusTitle.Background;
    }

    #endregion Protected Methods

    #region Private Fields

    private delegate void ConvertHandler();

    private bool _isConverting;
    private bool _isConversionError;

    /// <summary>
    ///     Only one observer is expected!
    /// </summary>
    private Brush _titleBkDefault;

    private IObserver _observer;
    private ConverterOptions _options;

    private DirectoryConverterOutput _converterOutput;

    private ConverterCommandLines _commandLines;

    #endregion Private Fields

    #region Public Properties

    public ConverterOptions Options
    {
        get => _options;
        set
        {
            _options = value;

            if (_options != null) _options.PropertyChanged += OnOptionsPropertyChanged;
        }
    }

    public Frame ParentFrame { get; set; }

    #endregion Public Properties

    #region Private Event Handlers

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        var theApp = (MainApplication)Application.Current;
        Debug.Assert(theApp != null);
        if (theApp != null && _commandLines == null)
        {
            _commandLines = theApp.CommandLines;
            if (_commandLines != null && !_commandLines.IsEmpty)
            {
                // this will remove the watermark...
                txtSourceDir.Focus();

                var sourceDir = _commandLines.SourceDir;
                if (!string.IsNullOrWhiteSpace(sourceDir) && Directory.Exists(sourceDir)) txtSourceDir.Text = sourceDir;
                txtOutputDir.Text = _commandLines.OutputDir;
                chkRecursive.IsChecked = _commandLines.Recursive;
                chkContinueOnError.IsChecked = _commandLines.ContinueOnError;
            }
        }

        Debug.Assert(_options != null);

        if (!_isConversionError) UpdateStatus();
    }

    private void OnDirTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateStatus();
    }

    private void OnSourceDirClick(object sender, RoutedEventArgs e)
    {
        var sourceDir = txtSourceDir.Text.Trim();
        if (string.IsNullOrWhiteSpace(sourceDir) || Directory.Exists(sourceDir) == false)
            sourceDir = Environment.CurrentDirectory;

        var selectedDirectory =
            DirectoryHelper.OpenFolderDialog(sourceDir, "Select the source directory of the SVG files");
        if (!string.IsNullOrWhiteSpace(selectedDirectory))
        {
            // this will remove the watermark...
            txtSourceDir.Focus();
            txtSourceDir.Text = selectedDirectory;
        }

        //FolderBrowserDialog dlg = new FolderBrowserDialog();
        //dlg.ShowNewFolderButton = true;
        //dlg.Description = "Select the source directory of the SVG files.";
        //string sourceDir = txtSourceDir.Text.Trim();
        //if (!string.IsNullOrWhiteSpace(sourceDir) &&
        //    Directory.Exists(sourceDir))
        //{
        //    dlg.SelectedPath = sourceDir;
        //}
        //else
        //{
        //    dlg.SelectedPath = Environment.CurrentDirectory;
        //}

        //dlg.RootFolder = Environment.SpecialFolder.MyComputer;

        //if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        //{
        //    // this will remove the watermark...
        //    txtSourceDir.Focus();
        //    txtSourceDir.Text = dlg.SelectedPath;
        //}
    }

    private void OnOutputDirClick(object sender, RoutedEventArgs e)
    {
        var sourceDir = txtSourceDir.Text.Trim();
        if (string.IsNullOrWhiteSpace(sourceDir) || Directory.Exists(sourceDir) == false)
            sourceDir = Environment.CurrentDirectory;
        var selectedDirectory =
            DirectoryHelper.OpenFolderDialog(sourceDir, "Select the output directory for the converted file");
        if (!string.IsNullOrWhiteSpace(selectedDirectory))
        {
            // this will remove the watermark...
            txtOutputDir.Focus();
            txtOutputDir.Text = selectedDirectory;
        }

        //FolderBrowserDialog dlg = new FolderBrowserDialog();
        //dlg.ShowNewFolderButton = true;
        //dlg.Description         = "Select the output directory for the converted file.";
        //string sourceDir = txtSourceDir.Text.Trim();
        //if (!string.IsNullOrWhiteSpace(sourceDir) &&
        //    Directory.Exists(sourceDir))
        //{
        //    dlg.SelectedPath = sourceDir;
        //}
        //else
        //{
        //    dlg.SelectedPath = Environment.CurrentDirectory;
        //}

        //dlg.RootFolder = Environment.SpecialFolder.MyComputer;

        //if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        //{
        //    // this will remove the watermark...
        //    txtOutputDir.Focus();
        //    txtOutputDir.Text = dlg.SelectedPath;
        //}
    }

    private void OnConvertClick(object sender, RoutedEventArgs e)
    {
        Debug.Assert(ParentFrame != null);
        if (ParentFrame == null) return;
        _isConverting = true;
        _isConversionError = false;
        btnConvert.IsEnabled = false;

        if (_converterOutput == null) _converterOutput = new DirectoryConverterOutput();
        _converterOutput.Options = _options;
        if (chkRecursive.IsChecked != null) _converterOutput.Recursive = chkRecursive.IsChecked.Value;
        if (chkContinueOnError.IsChecked != null) _converterOutput.ContinueOnError = chkContinueOnError.IsChecked.Value;
        _converterOutput.Subscribe(this);

        _converterOutput.SourceDir = txtSourceDir.Text;
        _converterOutput.OutputDir = txtOutputDir.Text;

        ParentFrame.Content = _converterOutput;

        //_converterOutput.Convert();
        Dispatcher.BeginInvoke(DispatcherPriority.Normal,
            new ConvertHandler(_converterOutput.Convert));
    }

    private void OnOptionsPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        _isConversionError = false;
    }

    #endregion Private Event Handlers

    #region Private Methods

    private void UpdateStatus()
    {
        if (_isConverting)
        {
            UpdateStatus("Converting",
                "The conversion process is currently running, please wait...", false);
            return;
        }

        var isValid = false;

        if (_options.IsValid)
        {
            var sourceDir = txtSourceDir.Text.Trim();
            var outputDir = txtOutputDir.Text.Trim();
            var isReadOnlyOutputDir = false;
            if (!string.IsNullOrWhiteSpace(outputDir))
                try
                {
                    var rootDir = Path.GetPathRoot(outputDir);
                    if (!string.IsNullOrWhiteSpace(rootDir))
                    {
                        var drive = new DriveInfo(rootDir);
                        if (!drive.IsReady || drive.DriveType == DriveType.CDRom
                                           || drive.DriveType == DriveType.Unknown)
                            isReadOnlyOutputDir = true;
                    }
                }
                catch
                {
                }

            if (string.IsNullOrWhiteSpace(sourceDir))
            {
                UpdateStatus("Conversion: Not Ready",
                    "Select an input directory of SVG files for conversion.", false);
            }
            else if (Directory.Exists(sourceDir))
            {
                if (isReadOnlyOutputDir)
                {
                    UpdateStatus("Error: Output Directory",
                        "The output directory is either invalid or read-only. Please select a different output directory.",
                        true);
                }
                else
                {
                    var isReadOnlySource = false;
                    try
                    {
                        var rootDir = Path.GetPathRoot(outputDir);
                        if (!string.IsNullOrWhiteSpace(rootDir))
                        {
                            var drive = new DriveInfo(rootDir);
                            if (!drive.IsReady || drive.DriveType == DriveType.CDRom
                                               || drive.DriveType == DriveType.Unknown)
                                isReadOnlySource = true;
                        }
                    }
                    catch
                    {
                    }

                    if (isReadOnlySource && string.IsNullOrWhiteSpace(outputDir))
                    {
                        UpdateStatus("Required: Output Directory",
                            "For the read-only source directory, an output directory is required and must be specified.",
                            true);
                    }
                    else
                    {
                        UpdateStatus("Conversion: Ready",
                            "Click the Convert button to convert the SVG files in the source directory.", false);

                        isValid = true;
                    }
                }
            }
            else
            {
                UpdateStatus("Error: Source Directory",
                    "The specified source directory is either invalid or does not exists.",
                    true);
            }
        }
        else
        {
            UpdateStatus("Error: Options", _options.Message, true);
        }

        btnConvert.IsEnabled = isValid;
    }

    private void UpdateStatus(string title, string text, bool isError)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(text)) return;

        statusTitle.Background = isError ? Brushes.Red : _titleBkDefault;
        statusTitle.Foreground = isError ? Brushes.White : Brushes.Black;

        statusTitle.Text = title;
        statusText.Text = text;
    }

    #endregion Private Methods

    #region IObservable Members

    public void Cancel()
    {
        if (_converterOutput != null) _converterOutput.Cancel();
    }

    public void Subscribe(IObserver observer)
    {
        _observer = observer;
    }

    #endregion IObservable Members

    #region IObserver Members

    public void OnStarted(IObservable sender)
    {
        _isConverting = true;

        progressBar.Visibility = Visibility.Visible;

        UpdateStatus();

        if (_observer != null) _observer.OnStarted(this);
    }

    public void OnCompleted(IObservable sender, bool isSuccessful)
    {
        _isConverting = false;

        progressBar.Visibility = Visibility.Hidden;

        UpdateStatus();

        if (_observer != null) _observer.OnCompleted(this, isSuccessful);

        _isConversionError = isSuccessful ? false : true;

        if (isSuccessful)
            UpdateStatus("Conversion: Successful",
                "The conversion of the specified directory is completed successfully.", false);
        else
            UpdateStatus("Conversion: Failed",
                "The conversion of the specified directory failed, see the output for further information.", true);
    }

    #endregion IObserver Members
}