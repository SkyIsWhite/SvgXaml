using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SharpVectors.Renderers.Wpf;

namespace SharpVectors.Converters;

/// <summary>
///     Interaction logic for FileListConverterOutput.xaml
/// </summary>
public partial class FileListConverterOutput : Page, IObservable
{
    #region Constructors and Destructor

    public FileListConverterOutput()
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

        ContinueOnError = true;
    }

    #endregion

    #region Public Methods

    public void Convert()
    {
        txtOutput.Clear();

        btnCancel.IsEnabled = false;

        _errorFiles = new List<string>();

        try
        {
            AppendLine("Converting files, please wait...");

            Debug.Assert(SourceFiles != null && SourceFiles.Count != 0);
            if (!string.IsNullOrWhiteSpace(OutputDir))
            {
                _outputInfoDir = new DirectoryInfo(OutputDir);
                if (!_outputInfoDir.Exists) _outputInfoDir.Create();
            }

            AppendLine("Input Files:");
            for (var i = 0; i < SourceFiles.Count; i++) AppendLine(SourceFiles[i]);
            AppendLine(string.Empty);

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

    #endregion

    #region Private Fields

    private int _convertedCount;

    private List<string> _errorFiles;

    /// <summary>
    ///     Only one observer is expected!
    /// </summary>
    private IObserver _observer;

    private DirectoryInfo _outputInfoDir;

    private readonly FileSvgReader _fileReader;
    private readonly WpfDrawingSettings _wpfSettings;

    private readonly BackgroundWorker _worker;

    #endregion

    #region Public Properties

    public ConverterOptions Options { get; set; }

    public IList<string> SourceFiles { get; set; }

    public string OutputDir { get; set; }

    public bool ContinueOnError { get; set; }

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

    #endregion

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

    #endregion

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

            if (!string.IsNullOrWhiteSpace(OutputDir)) builder.AppendLine("Output Directory: " + OutputDir);

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

        ConvertFiles(e, _outputInfoDir);

        if (!e.Cancel) e.Result = "Successful";
    }

    #endregion

    #endregion

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

    private void ConvertFiles(DoWorkEventArgs e, DirectoryInfo target)
    {
        _fileReader.FallbackOnWriterError = FallbackOnWriterError;

        if (e.Cancel) return;

        if (_worker.CancellationPending)
        {
            e.Cancel = true;
            return;
        }

        var outputDir = target;

        foreach (var svgFileName in SourceFiles)
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
                    if (_worker.CancellationPending)
                    {
                        e.Cancel = true;
                        break;
                    }

                    if (target == null)
                        outputDir = new DirectoryInfo(
                            Path.GetDirectoryName(svgFileName));

                    var drawing = _fileReader.Read(svgFileName,
                        outputDir);

                    if (drawing == null)
                        if (ContinueOnError)
                            throw new InvalidOperationException(
                                "The conversion failed due to unknown error.");

                    if (drawing != null && Options.GenerateImage)
                        _fileReader.SaveImage(svgFileName, target,
                            Options.EncoderType);

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

    #endregion

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

    #endregion
}