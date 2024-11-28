using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using SharpVectors.Renderers.Wpf;

namespace SharpVectors.Converters;

public sealed class ConsoleFilesConverter : ConsoleConverter
{
    #region Constructors and Destructor

    public ConsoleFilesConverter(IList<string> sourceFiles)
    {
        SourceFiles = sourceFiles;

        _wpfSettings = new WpfDrawingSettings();
        _wpfSettings.CultureInfo = _wpfSettings.NeutralCultureInfo;

        _fileReader = new FileSvgReader(_wpfSettings);
        _fileReader.SaveXaml = false;
        _fileReader.SaveZaml = false;

        _worker = new ConsoleWorker();
        //_worker.WorkerReportsProgress = true;
        //_worker.WorkerSupportsCancellation = true;

        _worker.DoWork += OnWorkerDoWork;
        _worker.RunWorkerCompleted += OnWorkerCompleted;
        _worker.ProgressChanged += OnWorkerProgressChanged;

        ContinueOnError = true;
    }

    #endregion

    #region Public Methods

    public override bool Convert(ConsoleWriter writer)
    {
        Debug.Assert(writer != null);

        Debug.Assert(SourceFiles != null && SourceFiles.Count != 0);
        if (SourceFiles == null || SourceFiles.Count == 0) return false;

        _writer = writer;

        _errorFiles = new List<string>();

        try
        {
            AppendLine(string.Empty);
            AppendLine("Converting files, please wait...");

            var outputDir = OutputDir;
            Debug.Assert(SourceFiles != null && SourceFiles.Count != 0);
            if (SourceFiles == null || SourceFiles.Count == 0) return false;
            if (!string.IsNullOrWhiteSpace(outputDir))
            {
                _outputInfoDir = new DirectoryInfo(outputDir);
                if (!_outputInfoDir.Exists) _outputInfoDir.Create();
            }

            AppendLine("Input Files:");
            for (var i = 0; i < SourceFiles.Count; i++) AppendLine(SourceFiles[i]);
            AppendLine(string.Empty);

            _worker.RunWorkerAsync();

            if (_observer != null) _observer.OnStarted(this);

            return true;
        }
        catch (Exception ex)
        {
            var builder = new StringBuilder();
            builder.AppendFormat("Error: Exception ({0})", ex.GetType());
            builder.AppendLine();
            builder.AppendLine(ex.Message);

            AppendText(builder.ToString());

            return false;
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

    private readonly ConsoleWorker _worker;
    private ConsoleWriter _writer;

    #endregion

    #region Public Properties

    public IList<string> SourceFiles { get; set; }

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

    #region ConsoleWorker Methods

    private void OnWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
    {
        if (e.UserState != null) AppendLine(e.UserState.ToString());
    }

    private void OnWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
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

            var outputDir = OutputDir;
            if (!string.IsNullOrWhiteSpace(outputDir)) builder.AppendLine("Output Directory: " + outputDir);

            if (_observer != null) _observer.OnCompleted(this, isSuccessful);
        }

        AppendLine(builder.ToString());
    }

    private void OnWorkerDoWork(object sender, DoWorkEventArgs e)
    {
        var worker = (ConsoleWorker)sender;

        var options = Options;

        _wpfSettings.IncludeRuntime = options.IncludeRuntime;
        _wpfSettings.TextAsGeometry = options.TextAsGeometry;

        _fileReader.UseFrameXamlWriter = !options.UseCustomXamlWriter;

        if (options.GeneralWpf)
        {
            _fileReader.SaveXaml = options.SaveXaml;
            _fileReader.SaveZaml = options.SaveZaml;
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

        _writer.Write(text);
    }

    private void AppendLine(string text)
    {
        if (text == null) return;

        _writer.WriteLine(text);
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

            var options = Options;

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

                    if (drawing != null && options.GenerateImage)
                        _fileReader.SaveImage(svgFileName, target,
                            options.EncoderType);

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

    public override void Cancel()
    {
        if (_worker != null)
            if (_worker.IsBusy)
            {
                _worker.CancelAsync();

                // Wait for the ConsoleWorker to finish the download.
                while (_worker.IsBusy)
                    // Keep UI messages moving, so the form remains 
                    // responsive during the asynchronous operation.
                    MainApplication.DoEvents();
            }
    }

    public override void Subscribe(IObserver observer)
    {
        _observer = observer;
    }

    #endregion
}