using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using SharpVectors.Converters.Properties;

namespace SharpVectors.Converters;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, IObserver
{
    #region Constructors and Destructor

    public MainWindow()
    {
        InitializeComponent();

        MinWidth = 640;
        MinHeight = 700;

        Width = 720;
        Height = 700;

        _startTabIndex = 0;

        _options = new ConverterOptions();
        var theApp = (MainApplication)Application.Current;
        Debug.Assert(theApp != null);
        if (theApp != null)
        {
            var commandLines = theApp.CommandLines;
            if (commandLines != null)
            {
                if (commandLines.IsEmpty)
                {
                    IList<string> sources = commandLines.Arguments;
                    DisplayHelp = commandLines.ShowHelp ||
                                  (sources != null && sources.Count != 0);
                }
                else
                {
                    _options.Update(commandLines);
                }
            }
            else
            {
                DisplayHelp = true;
            }

            if (!DisplayHelp)
            {
                var sourceFile = commandLines.SourceFile;
                if (!string.IsNullOrWhiteSpace(sourceFile) && File.Exists(sourceFile))
                {
                    _startTabIndex = 1;
                }
                else
                {
                    var sourceDir = commandLines.SourceDir;
                    if (!string.IsNullOrWhiteSpace(sourceDir) && Directory.Exists(sourceDir))
                    {
                        _startTabIndex = 3;
                    }
                    else
                    {
                        var sourceFiles = commandLines.SourceFiles;
                        if (sourceFiles != null && sourceFiles.Count != 0) _startTabIndex = 2;
                    }
                }
            }
        }

        _filesPage = new FileConverterPage();
        _filesPage.Options = _options;
        _filesPage.ParentFrame = filesFrame;
        _filesPage.Subscribe(this);

        filesFrame.Content = _filesPage;

        _filesListPage = new FileListConverterPage();
        _filesListPage.Options = _options;
        _filesListPage.ParentFrame = filesListFrame;
        _filesListPage.Subscribe(this);

        filesListFrame.Content = _filesListPage;

        _directoriesPage = new DirectoryConverterPage();
        _directoriesPage.Options = _options;
        _directoriesPage.ParentFrame = directoriesFrame;
        _directoriesPage.Subscribe(this);

        directoriesFrame.Content = _directoriesPage;

        _optionsPage = new OptionsPage();
        _optionsPage.Options = _options;

        optionsFrame.Content = _optionsPage;

        Loaded += OnWindowLoaded;
        Unloaded += OnWindowUnloaded;

        Closing += OnWindowClosing;
    }

    #endregion

    #region Public Properties

    public bool DisplayHelp { get; set; }

    #endregion

    #region Private Fields

    private readonly int _startTabIndex;
    private int _operationCount;
    private readonly ConverterOptions _options;

    private readonly OptionsPage _optionsPage;
    private readonly FileConverterPage _filesPage;
    private readonly FileListConverterPage _filesListPage;
    private readonly DirectoryConverterPage _directoriesPage;

    #endregion

    #region Private Event Handlers

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (DisplayHelp)
        {
            var helpItem = (TabItem)tabSteps.Items[5];
            helpItem.IsSelected = true;
            DisplayHelp = false;
        }
        else
        {
            var helpItem = (TabItem)tabSteps.Items[_startTabIndex];
            helpItem.IsSelected = true;
        }

        tabSteps.Focus();
    }

    private void OnWindowUnloaded(object sender, RoutedEventArgs e)
    {
    }

    private void OnWindowClosing(object sender, CancelEventArgs e)
    {
        try
        {
            Settings.Default.Save();
        }
        catch
        {
        }

        try
        {
            if (_operationCount > 0)
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

                if (_filesPage != null) _filesPage.Cancel();
                if (_filesListPage != null) _filesListPage.Cancel();
                if (_directoriesPage != null) _directoriesPage.Cancel();
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
        _operationCount++;

        if (sender == _filesPage)
            filesProgressBar.Visibility = Visibility.Visible;
        else if (sender == _filesListPage)
            filesListProgressBar.Visibility = Visibility.Visible;
        else if (sender == _directoriesPage) dirsProgressBar.Visibility = Visibility.Visible;
    }

    public void OnCompleted(IObservable sender, bool isSuccessful)
    {
        _operationCount--;
        Debug.Assert(_operationCount >= 0);

        if (sender == _filesPage)
            filesProgressBar.Visibility = Visibility.Hidden;
        else if (sender == _filesListPage)
            filesListProgressBar.Visibility = Visibility.Hidden;
        else if (sender == _directoriesPage) dirsProgressBar.Visibility = Visibility.Hidden;
    }

    #endregion
}