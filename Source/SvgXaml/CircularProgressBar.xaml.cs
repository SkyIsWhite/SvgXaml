// Yet another Circular ProgressBar control for WPF
// http://blogs.u2u.be/diederik/post/2010/02/26/Yet-another-Circular-ProgressBar-control-for-WPF.aspx
// By U2UConsult

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SharpVectors.Converters;

/// <summary>
///     Spinning Busy Indicator Control.
/// </summary>
public partial class CircularProgressBar
{
    /// <summary>
    ///     Startup time in milliseconds, default is a second.
    /// </summary>
    public static readonly DependencyProperty StartupDelayProperty =
        DependencyProperty.Register(
            "StartupDelay",
            typeof(int),
            typeof(CircularProgressBar),
            new PropertyMetadata(1000));

    /// <summary>
    ///     Spinning Speed. Default is 60, that's one rotation per second.
    /// </summary>
    public static readonly DependencyProperty RotationsPerMinuteProperty =
        DependencyProperty.Register(
            "RotationsPerMinute",
            typeof(double),
            typeof(CircularProgressBar),
            new PropertyMetadata(60.0));

    /// <summary>
    ///     Timer for the Animation.
    /// </summary>
    private readonly DispatcherTimer animationTimer;

    /// <summary>
    ///     Mouse Cursor.
    /// </summary>
    private Cursor originalCursor;

    /// <summary>
    ///     Initializes a new instance of the CircularProgressBar class.
    /// </summary>
    public CircularProgressBar()
    {
        InitializeComponent();

        animationTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher);
    }

    /// <summary>
    ///     Gets or sets the startup time in miliseconds, default is a second.
    /// </summary>
    public int StartupDelay
    {
        get => (int)GetValue(StartupDelayProperty);

        set => SetValue(StartupDelayProperty, value);
    }

    /// <summary>
    ///     Gets or sets the spinning speed. Default is 60, that's one rotation per second.
    /// </summary>
    public double RotationsPerMinute
    {
        get => (double)GetValue(RotationsPerMinuteProperty);

        set => SetValue(RotationsPerMinuteProperty, value);
    }

    /// <summary>
    ///     Startup Delay.
    /// </summary>
    private void StartDelay()
    {
        originalCursor = Mouse.OverrideCursor;
        Mouse.OverrideCursor = Cursors.Wait;

        // Startup
        animationTimer.Interval = new TimeSpan(0, 0, 0, 0, StartupDelay);
        animationTimer.Tick += StartSpinning;
        animationTimer.Start();
    }

    /// <summary>
    ///     Start Spinning.
    /// </summary>
    /// <param name="sender">Sender of the event.</param>
    /// <param name="e">Event Arguments.</param>
    private void StartSpinning(object sender, EventArgs e)
    {
        animationTimer.Stop();
        animationTimer.Tick -= StartSpinning;

        // 60 secs per minute, 1000 millisecs per sec, 10 rotations per full circle:
        animationTimer.Interval = new TimeSpan(0, 0, 0, 0, (int)(6000 / RotationsPerMinute));
        animationTimer.Tick += HandleAnimationTick;
        animationTimer.Start();
        Opacity = 1;

        Mouse.OverrideCursor = originalCursor;
    }

    /// <summary>
    ///     The control became invisible: stop spinning (animation consumes CPU).
    /// </summary>
    private void StopSpinning()
    {
        animationTimer.Stop();
        animationTimer.Tick -= HandleAnimationTick;
        Opacity = 0;
    }

    /// <summary>
    ///     Apply a single rotation transformation.
    /// </summary>
    /// <param name="sender">Sender of the Event: the Animation Timer.</param>
    /// <param name="e">Event arguments.</param>
    private void HandleAnimationTick(object sender, EventArgs e)
    {
        SpinnerRotate.Angle = (SpinnerRotate.Angle + 36) % 360;
    }

    /// <summary>
    ///     Control was loaded: distribute circles.
    /// </summary>
    /// <param name="sender">Sender of the Event: I wish I knew.</param>
    /// <param name="e">Event arguments.</param>
    private void HandleLoaded(object sender, RoutedEventArgs e)
    {
        SetPosition(C0, 0.0);
        SetPosition(C1, 1.0);
        SetPosition(C2, 2.0);
        SetPosition(C3, 3.0);
        SetPosition(C4, 4.0);
        SetPosition(C5, 5.0);
        SetPosition(C6, 6.0);
        SetPosition(C7, 7.0);
        SetPosition(C8, 8.0);
    }

    /// <summary>
    ///     Calculate position of a circle.
    /// </summary>
    /// <param name="ellipse">The circle.</param>
    /// <param name="sequence">Sequence number of the circle.</param>
    private void SetPosition(Ellipse ellipse, double sequence)
    {
        ellipse.SetValue(
            Canvas.LeftProperty,
            50.0 + Math.Sin(Math.PI * (0.2 * sequence + 1)) * 50.0);

        ellipse.SetValue(
            Canvas.TopProperty,
            50 + Math.Cos(Math.PI * (0.2 * sequence + 1)) * 50.0);
    }

    /// <summary>
    ///     Control was unloaded: stop spinning.
    /// </summary>
    /// <param name="sender">Sender of the event.</param>
    /// <param name="e">Event arguments.</param>
    private void HandleUnloaded(object sender, RoutedEventArgs e)
    {
        StopSpinning();
    }

    /// <summary>
    ///     Visibility property was changed: start or stop spinning.
    /// </summary>
    /// <param name="sender">Sender of the event.</param>
    /// <param name="e">Event arguments.</param>
    private void HandleVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Don't give the developer a headache.
        if (DesignerProperties.GetIsInDesignMode(this)) return;

        var isVisible = (bool)e.NewValue;

        if (isVisible)
            StartDelay();
        else
            StopSpinning();
    }
}