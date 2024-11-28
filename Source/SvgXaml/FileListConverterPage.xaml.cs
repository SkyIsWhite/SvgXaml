using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
///     Interaction logic for FileListConverterPage.xaml
/// </summary>
public partial class FileListConverterPage : Page, IObservable, IObserver
{
    #region Constructors and Destructor

    public FileListConverterPage()
    {
        InitializeComponent();

        // Reset the dimensions...
        Width = double.NaN;
        Height = double.NaN;

        Loaded += OnPageLoaded;

        if (_titleBkDefault == null &&
            statusTitle != null && statusTitle.IsInitialized)
            _titleBkDefault = statusTitle.Background;

        _listItems = new FileList();

        lstSourceFile.ItemsSource = _listItems;

        _listItems.CollectionChanged += OnSourceUpdated;
    }

    #endregion Constructors and Destructor

    #region Protected Methods

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        if (_titleBkDefault == null) _titleBkDefault = statusTitle.Background;
    }

    #endregion Protected Methods

    #region FileList Class

    public sealed class FileList : ObservableCollection<ListBoxItem>
    {
        #region Private Fields

        private readonly List<string> _listItems;

        #endregion Private Fields

        #region Constructors and Destructor

        public FileList()
        {
            _listItems = new List<string>();
        }

        #endregion Constructors and Destructor

        #region Public Methods

        public void Add(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;
            var fileExt = Path.GetExtension(filePath);
            if (string.IsNullOrWhiteSpace(fileExt)) return;
            if (string.Equals(fileExt, ".svg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileExt, ".svgz", StringComparison.OrdinalIgnoreCase))
            {
                var listItem = new ListBoxItem();
                listItem.Content = filePath;
                Add(listItem);

                _listItems.Add(filePath);
            }
        }

        #endregion Public Methods

        #region Public Properties

        public IList<string> FileItems => _listItems;

        public string LastDirectory
        {
            get
            {
                if (_listItems.Count != 0)
                    return Path.GetDirectoryName(
                        _listItems[_listItems.Count - 1]);

                return string.Empty;
            }
        }

        public bool HasReadOnlyMedia
        {
            get
            {
                var isReadOnlyOutputDir = false;
                try
                {
                    for (var i = 0; i < _listItems.Count; i++)
                    {
                        var rootDir = Path.GetPathRoot(_listItems[i]);
                        if (!string.IsNullOrWhiteSpace(rootDir))
                        {
                            var drive = new DriveInfo(rootDir);
                            if (!drive.IsReady || drive.DriveType == DriveType.CDRom
                                               || drive.DriveType == DriveType.Unknown)
                            {
                                isReadOnlyOutputDir = true;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                }

                return isReadOnlyOutputDir;
            }
        }

        #endregion Public Properties

        #region Protected Methods

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);

            _listItems.RemoveAt(index);
        }

        protected override void ClearItems()
        {
            base.ClearItems();

            if (_listItems != null) _listItems.Clear();
        }

        #endregion Protected Methods
    }

    #endregion FileList Class

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

    private readonly FileList _listItems;

    private FileListConverterOutput _converterOutput;

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
                var sourceFiles = _commandLines.SourceFiles;
                if (sourceFiles != null && sourceFiles.Count != 0)
                {
                    // this will remove the watermark...
                    lstSourceFile.Focus();

                    for (var i = 0; i < sourceFiles.Count; i++) _listItems.Add(sourceFiles[i]);
                }

                txtOutputDir.Text = _commandLines.OutputDir;
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

    private void OnSourceFileDrop(object sender, DragEventArgs e)
    {
        if (e.Data is DataObject && ((DataObject)e.Data).ContainsFileDropList())
            foreach (var filePath in ((DataObject)e.Data).GetFileDropList())
                _listItems.Add(filePath);
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
        lstSourceFile.Focus();
    }

    private void OnSourceAddClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog();
        dlg.Multiselect = true;
        dlg.Filter = "SVG Files|*.svg;*.svgz";
        ;
        dlg.FilterIndex = 1;

        var isSelected = dlg.ShowDialog();

        if (isSelected != null && isSelected.Value)
        {
            // this will remove the watermark...
            lstSourceFile.Focus();

            foreach (var filePath in dlg.FileNames) _listItems.Add(filePath);
        }
    }

    private void OnSourceRemoveClick(object sender, RoutedEventArgs e)
    {
        if (_listItems == null || _listItems.Count == 0) return;

        var selIndex = lstSourceFile.SelectedIndex;
        if (selIndex < 0) return;

        _listItems.RemoveAt(selIndex);
    }

    private void OnSourceClearClick(object sender, RoutedEventArgs e)
    {
        if (_listItems == null || _listItems.Count == 0) return;

        _listItems.Clear();
    }

    private void OnSourceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_listItems != null && _listItems.Count > 0)
        {
            btnClearSourceFile.IsEnabled = true;
            btnRemoveSourceFile.IsEnabled = lstSourceFile.SelectedIndex >= 0;
        }
        else
        {
            btnClearSourceFile.IsEnabled = false;
            btnRemoveSourceFile.IsEnabled = false;
        }
    }

    private void OnSourceUpdated(object sender, EventArgs e)
    {
        if (_listItems != null && _listItems.Count > 0)
        {
            btnClearSourceFile.IsEnabled = true;
            btnRemoveSourceFile.IsEnabled = lstSourceFile.SelectedIndex >= 0;
        }
        else
        {
            btnClearSourceFile.IsEnabled = false;
            btnRemoveSourceFile.IsEnabled = false;
        }

        txtFileCount.Text = _listItems.Count.ToString();

        UpdateStatus();
    }

    private void OnOutputDirClick(object sender, RoutedEventArgs e)
    {
        var sourceFile = _listItems.LastDirectory;
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
        //dlg.Description = "Select the output directory for the converted files.";
        //string sourceFile = _listItems.LastDirectory;
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

        if (_converterOutput == null) _converterOutput = new FileListConverterOutput();
        _converterOutput.Options = _options;
        _converterOutput.Subscribe(this);

        if (chkContinueOnError.IsChecked != null) _converterOutput.ContinueOnError = chkContinueOnError.IsChecked.Value;
        _converterOutput.SourceFiles = _listItems.FileItems;
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
            var outputDir = txtOutputDir.Text.Trim();
            var isReadOnlyOutputDir = _listItems.HasReadOnlyMedia;
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

            if (_listItems == null || _listItems.Count == 0)
            {
                UpdateStatus("Conversion: Not Ready",
                    "Select the input SVG files for conversion.", false);
            }
            else
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
                            "Click the Convert button to convert the input files.", false);

                        isValid = true;
                    }
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