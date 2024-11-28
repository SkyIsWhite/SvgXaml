using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SharpVectors.Renderers.Wpf;

namespace SharpVectors.Converters;

/// <summary>
///     Interaction logic for DirectoryConverterOutput.xaml
/// </summary>
public partial class DirectoryConverterOutput : Page, IObservable
{
    #region Constructors and Destructor

    public DirectoryConverterOutput()
    {
        InitializeComponent();

        _wpfSettings = new WpfDrawingSettings();
        _wpfSettings.CultureInfo = _wpfSettings.NeutralCultureInfo;

        _fileReader = new FileSvgReader(_wpfSettings);
        _fileReader.SaveXaml = false;
        _fileReader.SaveZaml = false;

        _worker = new BackgroundWorker();
        _worker.WorkerReportsProgress = true;
        _worker.WorkerSupportsCancellation = true;

        _worker.DoWork += OnWorkerDoWork;
        _worker.RunWorkerCompleted += OnWorkerCompleted;
        _worker.ProgressChanged += OnWorkerProgressChanged;

        Overwrite = true;
        Recursive = true;
        ContinueOnError = true;
    }

    #endregion Constructors and Destructor

    #region Public Methods

    public void Convert()
    {
        txtOutput.Clear();

        btnCancel.IsEnabled = false;

        _errorFiles = new List<string>();

        try
        {
            AppendLine("Converting files, please wait...");
            AppendLine("Input Directory: " + SourceDir);

            Debug.Assert(SourceDir != null && SourceDir.Length != 0);
            if (string.IsNullOrWhiteSpace(OutputDir)) OutputDir = new string(SourceDir.ToCharArray());
            _sourceInfoDir = new DirectoryInfo(SourceDir);
            _outputInfoDir = new DirectoryInfo(OutputDir);

            _worker.RunWorkerAsync();

            if (_observer != null) _observer.OnStarted(this);

            btnCancel.IsEnabled = true;
        }
        catch (Exception ex)
        {
            var builder = new StringBuilder();
            builder.AppendFormat("Error: Exception ({0})", ex.GetType());
            builder.AppendLine();
            builder.AppendLine(ex.Message);

            AppendText(builder.ToString());
        }
    }

    #endregion Public Methods

    #region Private Fields

    private int _convertedCount;

    private List<string> _errorFiles;

    /// <summary>
    ///     Only one observer is expected!
    /// </summary>
    private IObserver _observer;

    private DirectoryInfo _sourceInfoDir;
    private DirectoryInfo _outputInfoDir;

    private readonly FileSvgReader _fileReader;
    private readonly WpfDrawingSettings _wpfSettings;

    private readonly BackgroundWorker _worker;

    #endregion Private Fields

    #region Public Properties

    public ConverterOptions Options { get; set; }

    public string SourceDir { get; set; }

    public string OutputDir { get; set; }

    public bool ContinueOnError { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the directory copying is
    ///     recursive, that is includes the sub-directories.
    /// </summary>
    /// <value>
    ///     This property is <see langword="true" /> if the sub-directories are
    ///     included in the directory copy; otherwise, it is <see langword="false" />.
    ///     The default is <see langword="true" />.
    /// </value>
    public bool Recursive { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether an existing file is overwritten.
    /// </summary>
    /// <value>
    ///     This property is <see langword="true" /> if existing file is overwritten;
    ///     otherwise, it is <see langword="false" />. The default is <see langword="true" />.
    /// </value>
    public bool Overwrite { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the security settings of the
    ///     copied file is retained.
    /// </summary>
    /// <value>
    ///     This property is <see langword="true" /> if the security settings of the
    ///     file is also copied; otherwise, it is <see langword="false" />. The
    ///     default is <see langword="false" />.
    /// </value>
    public bool IncludeSecurity { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the copy operation includes
    ///     hidden directories and files.
    /// </summary>
    /// <value>
    ///     This property is <see langword="true" /> if hidden directories and files
    ///     are included in the copy operation; otherwise, it is
    ///     <see langword="false" />. The default is <see langword="false" />.
    /// </value>
    public bool IncludeHidden { get; set; }

    /// <summary>
    ///     Gets a value indicating whether a writer error occurred when
    ///     using the custom XAML writer.
    /// </summary>
    /// <value>
    ///     This is <see langword="true" /> if an error occurred when using
    ///     the custom XAML writer; otherwise, it is <see langword="false" />.
    /// </value>
    public bool WriterErrorOccurred { get; private set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to fall back and use
    ///     the .NET Framework XAML writer when an error occurred in using the
    ///     custom writer.
    /// </summary>
    /// <value>
    ///     This is <see langword="true" /> if the converter falls back to using
    ///     the system XAML writer when an error occurred in using the custom
    ///     writer; otherwise, it is <see langword="false" />. If <see langword="false" />,
    ///     an exception, which occurred in using the custom writer will be
    ///     thrown. The default is <see langword="false" />.
    /// </value>
    public bool FallbackOnWriterError { get; set; }

    #endregion Public Properties

    #region Private Event Handlers

    #region Page Methods

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        var startCursor = Cursor;

        try
        {
            Cursor = Cursors.Wait;

            Cancel();
        }
        catch (Exception ex)
        {
            var builder = new StringBuilder();
            builder.AppendFormat("Error: Exception ({0})", ex.GetType());
            builder.AppendLine();
            builder.AppendLine(ex.Message);

            AppendText(builder.ToString());
        }
        finally
        {
            Cursor = startCursor;
        }
    }

    #endregion Page Methods

    #region BackgroundWorker Methods

    private void OnWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
    {
        if (e.UserState != null) AppendLine(e.UserState.ToString());
    }

    private void OnWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        btnCancel.IsEnabled = false;

        var builder = new StringBuilder();
        if (e.Error != null)
        {
            var ex = e.Error;

            if (ex != null)
            {
                builder.AppendFormat("Error: Exception ({0})", ex.GetType());
                builder.AppendLine();
                builder.AppendLine(ex.Message);
                builder.AppendLine(ex.ToString());
            }
            else
            {
                builder.AppendFormat("Error: Unknown");
            }

            if (_observer != null) _observer.OnCompleted(this, false);
        }
        else if (e.Cancelled)
        {
            builder.AppendLine("Result: Cancelled");

            if (_observer != null) _observer.OnCompleted(this, false);
        }
        else if (e.Result != null)
        {
            var resultText = e.Result.ToString();
            var isSuccessful = !string.IsNullOrWhiteSpace(resultText) &&
                               string.Equals(resultText, "Successful", StringComparison.OrdinalIgnoreCase);

            if (_errorFiles == null || _errorFiles.Count == 0)
            {
                builder.AppendLine("Total number of files converted: " + _convertedCount);
            }
            else
            {
                builder.AppendLine("Total number of files successful converted: " + _convertedCount);
                builder.AppendLine("Total number of files failed: " + _errorFiles.Count);
            }

            if (!string.IsNullOrWhiteSpace(resultText)) builder.AppendLine("Result: " + resultText);

            if (!string.IsNullOrWhiteSpace(OutputDir))
                builder.AppendLine("Output Directory: " + OutputDir);
            else if (_outputInfoDir != null) builder.AppendLine("Output Directory: " + _outputInfoDir.FullName);

            if (_observer != null) _observer.OnCompleted(this, isSuccessful);
        }

        AppendLine(builder.ToString());
    }

    private void OnWorkerDoWork(object sender, DoWorkEventArgs e)
    {
        var worker = (BackgroundWorker)sender;

        _wpfSettings.IncludeRuntime = Options.IncludeRuntime;
        _wpfSettings.TextAsGeometry = Options.TextAsGeometry;

        _fileReader.UseFrameXamlWriter = !Options.UseCustomXamlWriter;

        if (Options.GeneralWpf)
        {
            _fileReader.SaveXaml = Options.SaveXaml;
            _fileReader.SaveZaml = Options.SaveZaml;
        }
        else
        {
            _fileReader.SaveXaml = false;
            _fileReader.SaveZaml = false;
        }

        ProcessConversion(e, _sourceInfoDir, _outputInfoDir);

        if (!e.Cancel) e.Result = "Successful";
    }

    #endregion BackgroundWorker Methods

    #endregion Private Event Handlers

    #region Private Methods

    private void AppendText(string text)
    {
        if (text == null) return;

        txtOutput.AppendText(text);
    }

    private void AppendLine(string text)
    {
        if (text == null) return;

        txtOutput.AppendText(text + Environment.NewLine);
    }

    private void ProcessConversion(DoWorkEventArgs e, DirectoryInfo source,
        DirectoryInfo target)
    {
        if (e.Cancel) return;

        if (_worker.CancellationPending)
        {
            e.Cancel = true;
            return;
        }

        // Convert the files in the specified directory...
        ConvertFiles(e, source, target);

        if (e.Cancel) return;

        if (_worker.CancellationPending)
        {
            e.Cancel = true;
            return;
        }

        if (!Recursive) return;

        // If recursive, process any sub-directory...
        var arrSourceInfo = source.GetDirectories();

        var dirCount = arrSourceInfo == null ? 0 : arrSourceInfo.Length;

        for (var i = 0; i < dirCount; i++)
        {
            var sourceInfo = arrSourceInfo[i];
            var fileAttr = sourceInfo.Attributes;
            if (!IncludeHidden)
                if ((fileAttr & FileAttributes.Hidden) == FileAttributes.Hidden)
                    continue;

            if (e.Cancel) break;

            if (_worker.CancellationPending)
            {
                e.Cancel = true;
                break;
            }

            DirectoryInfo targetInfo = null;
            targetInfo = target.CreateSubdirectory(sourceInfo.Name);
            targetInfo.Attributes = fileAttr;

            ProcessConversion(e, sourceInfo, targetInfo);
        }
    }

    private void ConvertFiles(DoWorkEventArgs e, DirectoryInfo source,
        DirectoryInfo target)
    {
        _fileReader.FallbackOnWriterError = FallbackOnWriterError;

        if (e.Cancel) return;

        if (_worker.CancellationPending)
        {
            e.Cancel = true;
            return;
        }

        var fileIterator = DirectoryHelper.FindFiles(
            source, "*.*", SearchOption.TopDirectoryOnly);
        foreach (var svgFileName in fileIterator)
        {
            if (_worker.CancellationPending)
            {
                e.Cancel = true;
                break;
            }

            var fileExt = Path.GetExtension(svgFileName);
            if (string.Equals(fileExt, ".svg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileExt, ".svgz", StringComparison.OrdinalIgnoreCase))
                try
                {
                    var fileAttr = File.GetAttributes(svgFileName);
                    if (!IncludeHidden)
                        if ((fileAttr & FileAttributes.Hidden) == FileAttributes.Hidden)
                            continue;

                    FileSecurity security = null;
                    if (_worker.CancellationPending)
                    {
                        e.Cancel = true;
                        break;
                    }

                    var drawing = _fileReader.Read(svgFileName,
                        target);

                    if (drawing == null)
                        if (ContinueOnError)
                            throw new InvalidOperationException(
                                "The conversion failed due to unknown error.");

                    if (Options.SaveXaml)
                    {
                        var xamlFile = _fileReader.XamlFile;
                        if (!string.IsNullOrWhiteSpace(xamlFile) &&
                            File.Exists(xamlFile))
                            File.SetAttributes(xamlFile, fileAttr);
                    }

                    if (Options.SaveZaml)
                    {
                        var zamlFile = _fileReader.ZamlFile;
                        if (!string.IsNullOrWhiteSpace(zamlFile) &&
                            File.Exists(zamlFile))
                            File.SetAttributes(zamlFile, fileAttr);
                    }

                    if (drawing != null && Options.GenerateImage)
                    {
                        _fileReader.SaveImage(svgFileName, target,
                            Options.EncoderType);
                        var imageFile = _fileReader.ImageFile;
                        if (!string.IsNullOrWhiteSpace(imageFile) &&
                            File.Exists(imageFile))
                            File.SetAttributes(imageFile, fileAttr);
                    }

                    if (drawing != null) _convertedCount++;

                    if (_fileReader.WriterErrorOccurred) WriterErrorOccurred = true;
                }
                catch (Exception ex)
                {
                    _errorFiles.Add(svgFileName);

                    if (ContinueOnError)
                    {
                        var builder = new StringBuilder();
                        builder.AppendLine("Error converting: " + svgFileName);
                        builder.AppendFormat("Error: Exception ({0})", ex.GetType());
                        builder.AppendLine();
                        builder.AppendLine(ex.Message);
                        builder.AppendLine(ex.ToString());

                        _worker.ReportProgress(0, builder.ToString());
                    }
                    else
                    {
                        throw;
                    }
                }
        }
    }

    #endregion Private Methods

    #region IObservable Members

    public void Cancel()
    {
        btnCancel.IsEnabled = false;

        if (_worker != null)
            if (_worker.IsBusy)
            {
                _worker.CancelAsync();

                // Wait for the BackgroundWorker to finish the download.
                while (_worker.IsBusy)
                    // Keep UI messages moving, so the form remains
                    // responsive during the asynchronous operation.
                    MainApplication.DoEvents();
            }
    }

    public void Subscribe(IObserver observer)
    {
        _observer = observer;
    }

    #endregion IObservable Members
}