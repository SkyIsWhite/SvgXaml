using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Media;
using SharpVectors.Renderers.Wpf;

namespace SharpVectors.Converters;

public sealed class ConsoleFileConverter : ConsoleConverter
{
    #region Constructors and Destructor

    public ConsoleFileConverter(string sourceFile)
    {
        SourceFile = sourceFile;

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
    }

    #endregion

    #region Public Propeties

    public string SourceFile { get; }

    #endregion

    #region Public Methods

    public override bool Convert(ConsoleWriter writer)
    {
        Debug.Assert(writer != null);

        Debug.Assert(SourceFile != null && SourceFile.Length != 0);
        if (string.IsNullOrWhiteSpace(SourceFile) || !File.Exists(SourceFile)) return false;

        _writer = writer;

        try
        {
            AppendLine(string.Empty);
            AppendLine("Converting file, please wait...");
            AppendLine("Input File: " + SourceFile);

            var _outputDir = OutputDir;
            if (string.IsNullOrWhiteSpace(_outputDir)) _outputDir = Path.GetDirectoryName(SourceFile);
            _outputInfoDir = new DirectoryInfo(_outputDir);

            //this.OnConvert();

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

    private string _imageFile;
    private string _xamlFile;
    private string _zamlFile;

    private IObserver _observer;

    private DrawingGroup _drawing;

    private DirectoryInfo _outputInfoDir;

    private readonly FileSvgReader _fileReader;
    private readonly WpfDrawingSettings _wpfSettings;

    private ConsoleWriter _writer;

    private readonly ConsoleWorker _worker;

    #endregion

    #region Private Methods

    #region ConsoleWorker Methods

    private void OnWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
    {
    }

    private void OnWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        if (_drawing != null)
            if (!_drawing.IsFrozen)
                _drawing.Freeze();

        var isSuccessful = false;

        var builder = new StringBuilder();
        if (e.Error != null || _drawing == null)
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

            isSuccessful = false;
        }
        else if (e.Cancelled)
        {
            builder.AppendLine("Result: Cancelled");

            isSuccessful = false;
        }
        else if (e.Result != null)
        {
            var resultText = e.Result.ToString();
            if (!string.IsNullOrWhiteSpace(resultText)) builder.AppendLine("Result: " + resultText);

            builder.AppendLine("Output Files:");
            if (_xamlFile != null) builder.AppendLine(_xamlFile);
            if (_zamlFile != null) builder.AppendLine(_zamlFile);
            if (_imageFile != null) builder.AppendLine(_imageFile);

            isSuccessful = string.Equals(resultText, "Successful",
                StringComparison.OrdinalIgnoreCase);
        }

        AppendLine(builder.ToString());
        if (_observer != null) _observer.OnCompleted(this, isSuccessful);
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

        _drawing = _fileReader.Read(SourceFile, _outputInfoDir);

        if (_drawing == null)
        {
            e.Result = "Failed";
            return;
        }

        if (options.GenerateImage)
        {
            _fileReader.SaveImage(SourceFile, _outputInfoDir,
                options.EncoderType);

            _imageFile = _fileReader.ImageFile;
        }

        _xamlFile = _fileReader.XamlFile;
        _zamlFile = _fileReader.ZamlFile;

        if (_drawing.CanFreeze) _drawing.Freeze();

        e.Result = "Successful";
    }

    #endregion

    #region Other Methods

    private void OnSyncConvert()
    {
        var builder = new StringBuilder();
        try
        {
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

            Drawing drawing = _fileReader.Read(SourceFile, _outputInfoDir);

            if (drawing == null)
            {
                AppendLine("Result: Conversion Failed.");
            }
            else
            {
                string _imageFile = null;
                if (options.GenerateImage)
                {
                    _fileReader.SaveImage(SourceFile, _outputInfoDir,
                        options.EncoderType);

                    _imageFile = _fileReader.ImageFile;
                }

                var _xamlFile = _fileReader.XamlFile;
                var _zamlFile = _fileReader.ZamlFile;

                builder.AppendLine("Result: Conversion is Successful.");

                builder.AppendLine("Output Files:");
                if (_xamlFile != null) builder.AppendLine(_xamlFile);
                if (_zamlFile != null) builder.AppendLine(_zamlFile);
                if (_imageFile != null) builder.AppendLine(_imageFile);
            }
        }
        catch (Exception ex)
        {
            builder.AppendFormat("Error: Exception ({0})", ex.GetType());
            builder.AppendLine();
            builder.AppendLine(ex.Message);
            builder.AppendLine(ex.ToString());
        }

        AppendLine(builder.ToString());
    }

    private void AppendText(string text)
    {
        if (text == null) return;

        _writer.WriteLine(text);
    }

    private void AppendLine(string text)
    {
        if (text == null) return;

        _writer.WriteLine(text);
    }

    #endregion

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