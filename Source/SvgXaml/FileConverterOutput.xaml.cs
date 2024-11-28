using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using SharpVectors.Renderers.Wpf;

namespace SharpVectors.Converters;

/// <summary>
///     Interaction logic for FileConverterOutput.xaml
/// </summary>
public partial class FileConverterOutput : Page, IObservable
{
    #region Constructors and Destructor

    public FileConverterOutput()
    {
        InitializeComponent();

        textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
        var options = textEditor.Options;
        if (options != null)
        {
            options.AllowScrollBelowDocument = true;
            options.EnableHyperlinks = true;
            options.EnableEmailHyperlinks = true;
            //options.ShowSpaces = true;
            //options.ShowTabs = true;
            //options.ShowEndOfLine = true;              
        }

        textEditor.IsReadOnly = true;
        textEditor.ShowLineNumbers = true;

        _foldingManager = FoldingManager.Install(textEditor.TextArea);
        _foldingStrategy = new XmlFoldingStrategy();

        _wpfSettings = new WpfDrawingSettings();
        _wpfSettings.CultureInfo = _wpfSettings.NeutralCultureInfo;

        _fileReader = new FileSvgReader(_wpfSettings);
        _fileReader.SaveXaml = false;
        _fileReader.SaveZaml = false;

        mouseHandlingMode = MouseHandlingMode.None;

        _worker = new BackgroundWorker();
        _worker.WorkerReportsProgress = true;
        _worker.WorkerSupportsCancellation = true;

        _worker.DoWork += OnWorkerDoWork;
        _worker.RunWorkerCompleted += OnWorkerCompleted;
        _worker.ProgressChanged += OnWorkerProgressChanged;
    }

    #endregion

    #region Public Methods

    public void Convert()
    {
        txtOutput.Clear();

        _imageFile = null;
        _zamlFile = null;
        _xamlFile = null;
        _bitmapImage = null;
        _drawing = null;

        if (svgViewer != null) svgViewer.UnloadDiagrams();
        UnloadDocument();

        tabControl.SelectedIndex = 0;
        var xamlItem = (TabItem)tabControl.Items[2];
        xamlItem.Visibility = Options.GeneralWpf ? Visibility.Visible : Visibility.Collapsed;
        var imageItem = (TabItem)tabControl.Items[3];
        imageItem.Visibility = Options.GenerateImage ? Visibility.Visible : Visibility.Collapsed;

        try
        {
            AppendLine("Converting: " + SourceFile);

            Debug.Assert(SourceFile != null && SourceFile.Length != 0);
            if (string.IsNullOrWhiteSpace(OutputDir)) OutputDir = Path.GetDirectoryName(SourceFile);
            _outputInfoDir = new DirectoryInfo(OutputDir);

            _worker.RunWorkerAsync();

            if (_observer != null) _observer.OnStarted(this);
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

    private string _documentFile;

    private FoldingManager _foldingManager;
    private XmlFoldingStrategy _foldingStrategy;

    /// <summary>
    ///     Only one observer is expected!
    /// </summary>
    private IObserver _observer;

    private string _imageFile;
    private string _xamlFile;
    private string _zamlFile;
    private BitmapImage _bitmapImage;

    private DrawingGroup _drawing;

    private DirectoryInfo _outputInfoDir;

    private readonly FileSvgReader _fileReader;
    private readonly WpfDrawingSettings _wpfSettings;

    private readonly BackgroundWorker _worker;

    /// <summary>
    ///     Specifies the current state of the mouse handling logic.
    /// </summary>
    private MouseHandlingMode mouseHandlingMode;

    /// <summary>
    ///     The point that was clicked relative to the ZoomAndPanControl.
    /// </summary>
    private Point origZoomAndPanControlMouseDownPoint;

    /// <summary>
    ///     The point that was clicked relative to the content that is contained within the ZoomAndPanControl.
    /// </summary>
    private Point origContentMouseDownPoint;

    /// <summary>
    ///     Records which mouse button clicked during mouse dragging.
    /// </summary>
    private MouseButton mouseButtonDown;

    #endregion

    #region Public Properties

    public ConverterOptions Options { get; set; }

    public string SourceFile { get; set; }

    public string OutputDir { get; set; }

    #endregion

    #region Private Event Handlers

    #region Page Methods

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        //zoomSlider.Value = 100;

        if (zoomPanControl != null) zoomPanControl.IsMouseWheelScrollingEnabled = true;
    }

    private void OnTabSelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        var source = e.Source;
        var orisource = e.OriginalSource;

        switch (tabControl.SelectedIndex)
        {
            case 0:
                break;
            case 1:
                break;
            case 2:
                var selectItem = (TabItem)tabControl.Items[2];
                if (string.Equals(selectItem.Name, "xamlTabItem",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(_xamlFile) && File.Exists(_xamlFile))
                    {
                        if (string.IsNullOrWhiteSpace(_documentFile)) LoadDocument(_xamlFile);
                    }
                    else if (!string.IsNullOrWhiteSpace(_zamlFile) && File.Exists(_zamlFile))
                    {
                        if (string.IsNullOrWhiteSpace(_documentFile)) LoadDocument(_zamlFile);
                    }
                }
                else if (string.Equals(selectItem.Name, "imageTabItem",
                             StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(_imageFile) && File.Exists(_imageFile))
                        if (_bitmapImage == null)
                        {
                            _bitmapImage = new BitmapImage(new Uri(_imageFile));

                            svgImage.Source = _bitmapImage;
                        }
                }

                break;
            case 3:
                if (!string.IsNullOrWhiteSpace(_imageFile) && File.Exists(_imageFile))
                    if (_bitmapImage == null)
                    {
                        _bitmapImage = new BitmapImage(new Uri(_imageFile));

                        svgImage.Source = _bitmapImage;
                    }

                break;
        }
    }

    #endregion

    #region BackgroundWorker Methods

    private void OnWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
    {
    }

    private void OnWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        if (_drawing != null)
        {
            if (!_drawing.IsFrozen) _drawing.Freeze();

            svgViewer.RenderDiagrams(_drawing);
            //svgViewer.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
            //    new Action<DrawingGroup>(svgViewer.RenderDiagrams), _drawing);

            //Rect bounds = svgViewer.Bounds;

            //if (bounds.IsEmpty)
            //{
            //    bounds = new Rect(0, 0,
            //        canvasScroller.ActualWidth, canvasScroller.ActualHeight);
            //}
            //if (!bounds.IsEmpty)
            //{
            //    //zoomPanControl.AnimatedZoomTo(bounds);
            //}
        }

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
            if (!string.IsNullOrWhiteSpace(resultText)) builder.AppendLine("Result: " + resultText);

            builder.AppendLine("Output Files:");
            if (_xamlFile != null) builder.AppendLine(_xamlFile);
            if (_zamlFile != null) builder.AppendLine(_zamlFile);
            if (_imageFile != null) builder.AppendLine(_imageFile);

            if (_observer != null)
                _observer.OnCompleted(this, string.Equals(resultText, "Successful",
                    StringComparison.OrdinalIgnoreCase));
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

        _drawing = _fileReader.Read(SourceFile, _outputInfoDir);

        if (_drawing == null)
        {
            e.Result = "Failed";
            return;
        }

        if (Options.GenerateImage)
        {
            _fileReader.SaveImage(SourceFile, _outputInfoDir,
                Options.EncoderType);

            _imageFile = _fileReader.ImageFile;
        }

        _xamlFile = _fileReader.XamlFile;
        _zamlFile = _fileReader.ZamlFile;

        if (_drawing.CanFreeze) _drawing.Freeze();

        e.Result = "Successful";
    }

    #endregion

    #region Private Zoom Panel Handlers

    private void OnZoomSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (zoomPanControl != null)
        {
            //zoomPanControl.AnimatedZoomTo(zoomSlider.Value / 100.0);
        }
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e)
    {
        ZoomIn();
    }

    private void OnZoomOutClick(object sender, RoutedEventArgs e)
    {
        ZoomOut();
    }

    private void OnResetZoom(object sender, RoutedEventArgs e)
    {
        if (zoomPanControl == null) return;

        zoomPanControl.ContentScale = 1.0;
    }

    /// <summary>
    ///     The 'ZoomIn' command (bound to the plus key) was executed.
    /// </summary>
    private void OnZoomFitClick(object sender, RoutedEventArgs e)
    {
        if (svgViewer == null || zoomPanControl == null) return;

        var bounds = svgViewer.Bounds;

        //Rect rect = new Rect(0, 0,
        //    mainFrame.RenderSize.Width, mainFrame.RenderSize.Height);
        //Rect rect = new Rect(0, 0,
        //    bounds.Width, bounds.Height);
        if (bounds.IsEmpty)
            bounds = new Rect(0, 0,
                canvasScroller.ActualWidth, canvasScroller.ActualHeight);
        zoomPanControl.AnimatedZoomTo(bounds);
    }

    private void OnPanClick(object sender, RoutedEventArgs e)
    {
        //if (drawScrollView == null)
        //{
        //    return;
        //}

        //drawScrollView.ZoomableCanvas.IsPanning = 
        //    (tbbPanning.IsChecked != null && tbbPanning.IsChecked.Value);
    }

    /// <summary>
    ///     Event raised on mouse down in the ZoomAndPanControl.
    /// </summary>
    private void OnZoomPanMouseDown(object sender, MouseButtonEventArgs e)
    {
        svgViewer.Focus();
        Keyboard.Focus(svgViewer);

        mouseButtonDown = e.ChangedButton;
        origZoomAndPanControlMouseDownPoint = e.GetPosition(zoomPanControl);
        origContentMouseDownPoint = e.GetPosition(svgViewer);

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 &&
            (e.ChangedButton == MouseButton.Left ||
             e.ChangedButton == MouseButton.Right))
            // Shift + left- or right-down initiates zooming mode.
            mouseHandlingMode = MouseHandlingMode.Zooming;
        else if (mouseButtonDown == MouseButton.Left)
            // Just a plain old left-down initiates panning mode.
            mouseHandlingMode = MouseHandlingMode.Panning;

        if (mouseHandlingMode != MouseHandlingMode.None)
        {
            // Capture the mouse so that we eventually receive the mouse up event.
            zoomPanControl.CaptureMouse();
            e.Handled = true;
        }
    }

    /// <summary>
    ///     Event raised on mouse up in the ZoomAndPanControl.
    /// </summary>
    private void OnZoomPanMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (mouseHandlingMode != MouseHandlingMode.None)
        {
            if (mouseHandlingMode == MouseHandlingMode.Zooming)
            {
                if (mouseButtonDown == MouseButton.Left)
                    // Shift + left-click zooms in on the content.
                    ZoomIn();
                else if (mouseButtonDown == MouseButton.Right)
                    // Shift + left-click zooms out from the content.
                    ZoomOut();
            }

            zoomPanControl.ReleaseMouseCapture();
            mouseHandlingMode = MouseHandlingMode.None;
            e.Handled = true;
        }
    }

    /// <summary>
    ///     Event raised on mouse move in the ZoomAndPanControl.
    /// </summary>
    private void OnZoomPanMouseMove(object sender, MouseEventArgs e)
    {
        if (mouseHandlingMode == MouseHandlingMode.Panning)
        {
            //
            // The user is left-dragging the mouse.
            // Pan the viewport by the appropriate amount.
            //
            var curContentMousePoint = e.GetPosition(svgViewer);
            var dragOffset = curContentMousePoint - origContentMouseDownPoint;

            zoomPanControl.ContentOffsetX -= dragOffset.X;
            zoomPanControl.ContentOffsetY -= dragOffset.Y;

            e.Handled = true;
        }
    }

    /// <summary>
    ///     Event raised by rotating the mouse wheel
    /// </summary>
    private void OnZoomPanMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;

        if (e.Delta > 0)
            ZoomIn();
        else if (e.Delta < 0) ZoomOut();
    }

    /// <summary>
    ///     The 'ZoomIn' command (bound to the plus key) was executed.
    /// </summary>
    private void OnZoomFit(object sender, RoutedEventArgs e)
    {
        if (svgViewer == null || zoomPanControl == null) return;

        var bounds = svgViewer.Bounds;

        //Rect rect = new Rect(0, 0,
        //    mainFrame.RenderSize.Width, mainFrame.RenderSize.Height);
        //Rect rect = new Rect(0, 0,
        //    bounds.Width, bounds.Height);
        if (bounds.IsEmpty)
            bounds = new Rect(0, 0,
                canvasScroller.ActualWidth, canvasScroller.ActualHeight);
        zoomPanControl.AnimatedZoomTo(bounds);
    }

    /// <summary>
    ///     The 'ZoomIn' command (bound to the plus key) was executed.
    /// </summary>
    private void OnZoomIn(object sender, RoutedEventArgs e)
    {
        ZoomIn();
    }

    /// <summary>
    ///     The 'ZoomOut' command (bound to the minus key) was executed.
    /// </summary>
    private void OnZoomOut(object sender, RoutedEventArgs e)
    {
        ZoomOut();
    }

    /// <summary>
    ///     Zoom the viewport out by a small increment.
    /// </summary>
    private void ZoomOut()
    {
        if (zoomPanControl == null) return;

        zoomPanControl.ContentScale -= 0.1;
    }

    /// <summary>
    ///     Zoom the viewport in by a small increment.
    /// </summary>
    private void ZoomIn()
    {
        if (zoomPanControl == null) return;

        zoomPanControl.ContentScale += 0.1;
    }

    #endregion

    #endregion

    #region Private Methods

    private void LoadDocument(string documentFileName)
    {
        if (textEditor == null || string.IsNullOrWhiteSpace(documentFileName)) return;

        var fileExt = Path.GetExtension(documentFileName);
        if (string.Equals(fileExt, ".zaml", StringComparison.OrdinalIgnoreCase))
            using (var fileStream = File.OpenRead(documentFileName))
            {
                using (var zipStream =
                       new GZipStream(fileStream, CompressionMode.Decompress))
                {
                    // Text Editor does not work with this stream, so we read the data to memory stream...
                    var memoryStream = new MemoryStream();
                    // Use this method is used to read all bytes from a stream.
                    var totalCount = 0;
                    var bufferSize = 512;
                    var buffer = new byte[bufferSize];
                    while (true)
                    {
                        var bytesRead = zipStream.Read(buffer, 0, bufferSize);
                        if (bytesRead == 0) break;

                        memoryStream.Write(buffer, 0, bytesRead);
                        totalCount += bytesRead;
                    }

                    if (totalCount > 0) memoryStream.Position = 0;

                    textEditor.Load(memoryStream);

                    memoryStream.Close();
                }
            }
        else
            textEditor.Load(documentFileName);

        if (_foldingManager == null || _foldingStrategy == null)
        {
            _foldingManager = FoldingManager.Install(textEditor.TextArea);
            _foldingStrategy = new XmlFoldingStrategy();
        }

        _foldingStrategy.UpdateFoldings(_foldingManager, textEditor.Document);

        _documentFile = documentFileName;
    }

    private void UnloadDocument()
    {
        if (textEditor != null) textEditor.Document.Text = string.Empty;

        _documentFile = null;
    }

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

    #endregion

    #region IObservable Members

    public void Cancel()
    {
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