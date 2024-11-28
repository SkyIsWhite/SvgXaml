using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace SharpVectors.Converters;

/// <summary>
///     Interaction logic for ConverterWindow.xaml
/// </summary>
public partial class ConverterWindow : Window, IObserver
{
    #region Constructors and Destructor

    public ConverterWindow()
    {
        InitializeComponent();

        MinWidth = 640;
        MinHeight = 340;

        Width = 640;
        Height = 340;

        _options = new ConverterOptions();

        Loaded += OnWindowLoaded;
        Unloaded += OnWindowUnloaded;

        Closing += OnWindowClosing;
        ContentRendered += OnWindowContentRendered;
    }

    #endregion

    #region Private Fields

    private delegate void ConvertHandler();

    private bool _isConverting;

    private readonly ConverterOptions _options;

    private FileListConverterOutput _converterOutput;

    #endregion

    #region Private Event Handlers

    private void OnWindowContentRendered(object sender, EventArgs e)
    {
        if (_options == null || !_options.IsValid) return;

        var theApp = (MainApplication)Application.Current;
        Debug.Assert(theApp != null);
        if (theApp == null) return;
        var commandLines = theApp.CommandLines;
        Debug.Assert(commandLines != null);
        if (commandLines == null || commandLines.IsEmpty) return;
        var sourceFiles = commandLines.SourceFiles;
        if (sourceFiles == null || sourceFiles.Count == 0)
        {
            var sourceFile = commandLines.SourceFile;
            if (string.IsNullOrWhiteSpace(sourceFile) ||
                !File.Exists(sourceFile))
                return;
            sourceFiles = new List<string>();
            sourceFiles.Add(sourceFile);
        }

        _isConverting = true;

        if (_converterOutput == null) _converterOutput = new FileListConverterOutput();

        _options.Update(commandLines);

        _converterOutput.Options = _options;
        _converterOutput.Subscribe(this);

        _converterOutput.ContinueOnError = commandLines.ContinueOnError;
        _converterOutput.SourceFiles = sourceFiles;
        _converterOutput.OutputDir = commandLines.OutputDir;

        frameConverter.Content = _converterOutput;

        //_converterOutput.Convert();
        Dispatcher.BeginInvoke(DispatcherPriority.Normal,
            new ConvertHandler(_converterOutput.Convert));
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
    }

    private void OnWindowUnloaded(object sender, RoutedEventArgs e)
    {
    }

    private void OnWindowClosing(object sender, CancelEventArgs e)
    {
        try
        {
            if (_isConverting)
            {
                var builder = new StringBuilder();
                builder.AppendLine("Conversion process is running on the background.");
                builder.AppendLine("Do you want to stop the conversion process and close this application?");
                var boxResult = MessageBox.Show(builder.ToString(), Title,
                    MessageBoxButton.YesNo, MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (boxResult == MessageBoxResult.No)
                {
                    e.Cancel = false;
                    return;
                }

                if (_converterOutput != null) _converterOutput.Cancel();
            }
        }
        catch
        {
        }
    }

    private void OnClickClosed(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region IObserver Members

    public void OnStarted(IObservable sender)
    {
        progressBar.Visibility = Visibility.Visible;
    }

    public void OnCompleted(IObservable sender, bool isSuccessful)
    {
        progressBar.Visibility = Visibility.Hidden;

        _isConverting = false;
    }

    #endregion
}