using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SharpVectors.Converters;

/// <summary>
///     Interaction logic for FileConverterPage.xaml
/// </summary>
public partial class FileConverterPage : Page, IObservable, IObserver
{
    #region Constructors and Destructor

    public FileConverterPage()
    {
        InitializeComponent();

        // Reset the dimensions...
        Width = double.NaN;
        Height = double.NaN;

        Loaded += OnPageLoaded;

        if (_titleBkDefault == null &&
            statusTitle != null && statusTitle.IsInitialized)
            _titleBkDefault = statusTitle.Background;
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

    private FileConverterOutput _converterOutput;

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
                txtSourceFile.Focus();

                var sourceFile = _commandLines.SourceFile;
                if (!string.IsNullOrWhiteSpace(sourceFile) && File.Exists(sourceFile)) txtSourceFile.Text = sourceFile;
                txtOutputDir.Text = _commandLines.OutputDir;
            }
        }

        Debug.Assert(_options != null);

        if (!_isConversionError) UpdateStatus();
    }

    private void OnSourceOutputTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateStatus();
    }

    private void OnSourceFileDrop(object sender, DragEventArgs e)
    {
        if (e.Data is DataObject && ((DataObject)e.Data).ContainsFileDropList())
            foreach (var filePath in ((DataObject)e.Data).GetFileDropList())
            {
                txtSourceFile.Text = filePath;
                break; // only a single file conversion is supported...
            }
    }

    private void OnSourceFilePreviewDragEnter(object sender, DragEventArgs e)
    {
        var dropPossible = e.Data != null && ((DataObject)e.Data).ContainsFileDropList();
        if (dropPossible) e.Effects = DragDropEffects.Copy;
    }

    private void OnSourceFilePreviewDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        // this will remove the watermark...
        txtSourceFile.Focus();
    }

    private void OnSourceFileClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog();
        dlg.Multiselect = false;
        dlg.Filter = "SVG Files|*.svg;*.svgz";
        ;
        dlg.FilterIndex = 1;

        var isSelected = dlg.ShowDialog();

        if (isSelected != null && isSelected.Value)
        {
            // this will remove the watermark...
            txtSourceFile.Focus();
            txtSourceFile.Text = dlg.FileName;
        }
    }

    private void OnOutputDirClick(object sender, RoutedEventArgs e)
    {
        var sourceFile = txtSourceFile.Text.Trim();
        var sourceDir = Environment.CurrentDirectory;
        if (!string.IsNullOrWhiteSpace(sourceFile) && File.Exists(sourceFile))
            sourceDir = Path.GetDirectoryName(sourceFile);
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
        //string sourceFile       = txtSourceFile.Text.Trim();
        //if (!string.IsNullOrWhiteSpace(sourceFile) &&
        //    File.Exists(sourceFile))
        //{
        //    dlg.SelectedPath = Path.GetDirectoryName(sourceFile);
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

        if (_converterOutput == null) _converterOutput = new FileConverterOutput();
        _converterOutput.Options = _options;
        _converterOutput.Subscribe(this);

        _converterOutput.SourceFile = txtSourceFile.Text;
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
            var sourceFile = txtSourceFile.Text.Trim();
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

            if (string.IsNullOrWhiteSpace(sourceFile))
            {
                UpdateStatus("Conversion: Not Ready",
                    "Select an input SVG file for conversion.", false);
            }
            else if (File.Exists(sourceFile))
            {
                var fileExt = Path.GetExtension(sourceFile);
                if (string.IsNullOrWhiteSpace(fileExt) ||
                    (!string.Equals(fileExt, ".svg", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(fileExt, ".svgz", StringComparison.OrdinalIgnoreCase)))
                {
                    UpdateStatus("Error: Source File",
                        "The specified file is not a valid SVG file or the file extension is invalid.",
                        true);
                }
                else if (isReadOnlyOutputDir)
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
                        UpdateStatus("Conversion: Ready (Local File)",
                            "Click the Convert button to convert the input file.", false);

                        isValid = true;
                    }
                }
            }
            else
            {
                // First, we try check for web source file...
                Uri webUri;
                if (Uri.TryCreate(sourceFile, UriKind.Absolute, out webUri)
                    && (string.Equals(webUri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                        || string.Equals(webUri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
                {
                    if (string.IsNullOrWhiteSpace(outputDir))
                    {
                        UpdateStatus("Required: Output Directory",
                            "For the web source file, an output directory is required and must be specified.", true);
                    }
                    else if (isReadOnlyOutputDir)
                    {
                        UpdateStatus("Error: Output Directory",
                            "The output directory is either invalid or read-only. Please select a different output directory.",
                            true);
                    }
                    else
                    {
                        UpdateStatus("Conversion: Ready (Web File)",
                            "Click the Convert button to convert the input file or the Preview button to preview the output.",
                            false);

                        isValid = true;
                    }
                }
                else
                {
                    UpdateStatus("Error: Source File",
                        "The specified source file is either invalid or the file does not exists.",
                        true);
                }
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
                "The conversion of the specified file is completed successfully.", false);
        else
            UpdateStatus("Conversion: Failed",
                "The conversion of the specified file failed, see the output for further information.", true);
    }

    #endregion IObserver Members
}