using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace SharpVectors.Converters;

/// <summary>
///     Interaction logic for MainApplication.xaml
/// </summary>
public class MainApplication : Application
{
    public MainApplication()
    {
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        DispatcherUnhandledException += OnApplicationUnhandledException;
    }

    public ConverterCommandLines CommandLines { get; set; }

    public void InitializeComponent(bool mainWindow)
    {
        if (mainWindow)
            StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
        else
            StartupUri = new Uri("ConverterWindow.xaml", UriKind.Relative);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
    }

    private void OnApplicationUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (e.Exception != null)
            try
            {
                //WiringErrorWindow errorDlg = new WiringErrorWindow();
                //errorDlg.Owner = this.MainWindow;
                //errorDlg.Initialize(e.Exception);
                //errorDlg.ShowDialog();
            }
            catch
            {
            }

        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject == null) return;

        try
        {
            //WiringErrorWindow errorDlg = new WiringErrorWindow();
            //errorDlg.Owner = this.MainWindow;
            //errorDlg.Initialize(e.ExceptionObject);

            //errorDlg.ShowDialog();
        }
        catch
        {
        }
    }

    public static void DoEvents()
    {
        var frame = new DispatcherFrame(true);
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
            (SendOrPostCallback)delegate(object arg)
            {
                var f = arg as DispatcherFrame;
                f.Continue = false;
            }, frame);
        Dispatcher.PushFrame(frame);
    }
}