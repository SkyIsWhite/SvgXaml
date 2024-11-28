using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using SharpVectors.Renderers.Wpf;

namespace SharpVectors.Converters;

public sealed class ConsoleDirectoryConverter : ConsoleConverter
{
    #region Constructors and Destructor

    public ConsoleDirectoryConverter(string sourceDir)
    {
        SourceDir = sourceDir;

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

        Overwrite = true;
        Recursive = true;
        ContinueOnError = true;
    }

    #endregion Constructors and Destructor

    #region Public Methods

    public override bool Convert(ConsoleWriter writer)
    {
        if (string.IsNullOrWhiteSpace(SourceDir) || !Directory.Exists(SourceDir)) return false;

        _writer = writer;

        _errorFiles = new List<string>();

        var outputDir = OutputDir;

        try
        {
            AppendLine(string.Empty);
            AppendLine("Converting files, please wait...");
            AppendLine("Input Directory: " + SourceDir);

            Debug.Assert(SourceDir != null && SourceDir.Length != 0);
            if (string.IsNullOrWhiteSpace(outputDir)) outputDir = new string(SourceDir.ToCharArray());
            _sourceInfoDir = new DirectoryInfo(SourceDir);
            _outputInfoDir = new DirectoryInfo(outputDir);

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

    private readonly ConsoleWorker _worker;

    private ConsoleWriter _writer;

    #endregion Private Fields

    #region Public Propeties

    public string SourceDir { get; set; }

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

    #endregion Public Propeties

    #region ConsoleWorker Methods

    private void OnWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
    {
        if (e.UserState != null) AppendLine(e.UserState.ToString());
    }

    private void OnWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        var outputDir = OutputDir;

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

            if (!string.IsNullOrWhiteSpace(outputDir))
                builder.AppendLine("Output Directory: " + outputDir);
            else if (_outputInfoDir != null) builder.AppendLine("Output Directory: " + _outputInfoDir.FullName);

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

        ProcessConversion(e, _sourceInfoDir, _outputInfoDir);

        if (!e.Cancel) e.Result = "Successful";
    }

    #endregion ConsoleWorker Methods

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

        var options = Options;

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

                    if (options.SaveXaml)
                    {
                        var xamlFile = _fileReader.XamlFile;
                        if (!string.IsNullOrWhiteSpace(xamlFile) &&
                            File.Exists(xamlFile))
                            File.SetAttributes(xamlFile, fileAttr);
                    }

                    if (options.SaveZaml)
                    {
                        var zamlFile = _fileReader.ZamlFile;
                        if (!string.IsNullOrWhiteSpace(zamlFile) &&
                            File.Exists(zamlFile))
                            File.SetAttributes(zamlFile, fileAttr);
                    }

                    if (drawing != null && options.GenerateImage)
                    {
                        _fileReader.SaveImage(svgFileName, target,
                            options.EncoderType);
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

    #endregion IObservable Members
}