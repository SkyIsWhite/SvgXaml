using System;
using System.ComponentModel;

namespace SharpVectors.Converters;

/// <summary>
///     ConsoleWorker is a helper class for running asynchronous tasks.
/// </summary>
public sealed class ConsoleWorker
{
    #region Private Fields

    private int _count;
    private readonly int _maxCount;
    private readonly object _countProtector;

    private readonly AsyncCallback _workerCallback;

    private readonly DoWorkEventHandler _eventHandler;

    #endregion Private Fields

    #region Constructors and Destructor

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConsoleWorker" /> class.
    /// </summary>
    public ConsoleWorker()
        : this(1)
    {
    }

    public ConsoleWorker(int maximumCount)
    {
        _countProtector = new object();

        _maxCount = maximumCount;
        _workerCallback = OnRunWorkerCompleted;
        _eventHandler = OnDoWork;
    }

    #endregion Constructors and Destructor

    #region Public Events

    /// <summary>
    ///     Occurs when [do work].
    /// </summary>
    public event DoWorkEventHandler DoWork;

    /// <summary>
    ///     Occurs when [run worker completed].
    /// </summary>
    public event RunWorkerCompletedEventHandler RunWorkerCompleted;

    /// <summary>
    ///     Occurs when [progress changed].
    /// </summary>
    public event ProgressChangedEventHandler ProgressChanged;

    #endregion Public Events

    #region Public Properties

    /// <summary>
    ///     Gets a value indicating whether this instance is busy.
    /// </summary>
    /// <value><c>true</c> if this instance is busy; otherwise, <c>false</c>.</value>
    public bool IsBusy
    {
        get
        {
            lock (_countProtector)
            {
                if (_count >= _maxCount) return true;

                return false;
            }
        }
    }

    /// <summary>
    ///     Gets a value indicating whether [cancellation pending].
    /// </summary>
    /// <value><c>true</c> if [cancellation pending]; otherwise, <c>false</c>.</value>
    public bool CancellationPending { get; private set; }

    #endregion Public Properties

    #region Public Methods

    /// <summary>
    ///     Runs the worker async.
    /// </summary>
    /// <param name="abortIfBusy">if set to <c>true</c> [abort if busy].</param>
    public bool RunWorkerAsync(bool abortIfBusy)
    {
        return RunWorkerAsync(abortIfBusy, null);
    }

    /// <summary>
    ///     Runs the worker async.
    /// </summary>
    /// <param name="argument">The argument.</param>
    public bool RunWorkerAsync(object argument)
    {
        if (IsBusy) return false;
        _count++;

        var args = new DoWorkEventArgs(argument);
        _eventHandler.BeginInvoke(this, args, _workerCallback, args);

        return true;
    }

    public bool RunWorkerAsync()
    {
        if (IsBusy) return false;
        _count++;

        var args = new DoWorkEventArgs(null);
        _eventHandler.BeginInvoke(this, args, _workerCallback, args);

        return true;
    }

    public bool RunWorkerAsync(bool abortIfBusy, object argument)
    {
        if (abortIfBusy && IsBusy) return false;
        _count++;

        var args = new DoWorkEventArgs(argument);
        _eventHandler.BeginInvoke(this, args, _workerCallback, args);

        return true;
    }

    /// <summary>
    ///     Cancels the async.
    /// </summary>
    public void CancelAsync()
    {
        CancellationPending = true;
    }

    /// <summary>
    ///     Reports the progress.
    /// </summary>
    /// <param name="percentProgress">The percent progress.</param>
    public void ReportProgress(int percentProgress)
    {
        OnProgressChanged(new ProgressChangedEventArgs(percentProgress, null));
    }

    /// <summary>
    ///     Reports the progress.
    /// </summary>
    /// <param name="percentProgress">The percent progress.</param>
    /// <param name="userState">State of the user.</param>
    public void ReportProgress(int percentProgress, object userState)
    {
        OnProgressChanged(new ProgressChangedEventArgs(percentProgress, userState));
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    ///     Called when [do work].
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs" /> instance containing the event data.</param>
    private void OnDoWork(object sender, DoWorkEventArgs e)
    {
        if (e.Cancel) return;
        if (DoWork != null) DoWork(this, e);
    }

    /// <summary>
    ///     Raises the <see cref="E:ProgressChanged" /> event.
    /// </summary>
    /// <param name="e">The <see cref="System.ComponentModel.ProgressChangedEventArgs" /> instance containing the event data.</param>
    private void OnProgressChanged(ProgressChangedEventArgs e)
    {
        if (ProgressChanged != null) ProgressChanged(this, e);
    }

    /// <summary>
    ///     Called when [run worker completed].
    /// </summary>
    /// <param name="ar">The ar.</param>
    private void OnRunWorkerCompleted(IAsyncResult ar)
    {
        var args = (DoWorkEventArgs)ar.AsyncState;

        try
        {
            if (RunWorkerCompleted != null)
                RunWorkerCompleted(this, new RunWorkerCompletedEventArgs(args.Result,
                    null, args.Cancel));
        }
        catch (Exception ex)
        {
            if (RunWorkerCompleted != null)
                RunWorkerCompleted(this, new RunWorkerCompletedEventArgs(args.Result,
                    ex, args.Cancel));
        }

        _count--;
    }

    #endregion Private Methods
}