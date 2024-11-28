namespace SharpVectors.Converters;

public abstract class ConsoleConverter : IObservable
{
    #region Public Methods

    public abstract bool Convert(ConsoleWriter writer);

    #endregion

    #region Private Fields

    #endregion

    #region Constructors and Destructor

    #endregion

    #region Public Propeties

    public string OutputDir { get; set; }

    public ConverterOptions Options { get; set; }

    #endregion

    #region IObservable Members

    public abstract void Cancel();

    public abstract void Subscribe(IObserver observer);

    #endregion
}