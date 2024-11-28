using System;
using System.ComponentModel;

namespace SharpVectors.Converters;

[Serializable]
public sealed class ConverterOptions : ICloneable, INotifyPropertyChanged
{
    #region INotifyPropertyChanged Members

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Public Methods

    public void Update(ConverterCommandLines commands)
    {
        if (commands == null) return;

        _textAsGeometry = commands.TextAsGeometry;
        _includeRuntime = commands.IncludeRuntime;
        _saveXaml = commands.SaveXaml;
        _saveZaml = commands.SaveZaml;
        _generateImage = commands.SaveImage;
        _generalWpf = _saveXaml || _saveZaml;
        _customXamlWriter = commands.UseCustomXamlWriter;
        if (_generateImage)
            switch (commands.Image.ToLower())
            {
                case "bmp":
                    _encoderType = ImageEncoderType.BmpBitmap;
                    break;
                case "png":
                    _encoderType = ImageEncoderType.PngBitmap;
                    break;
                case "jpeg":
                case "jpg":
                    _encoderType = ImageEncoderType.JpegBitmap;
                    break;
                case "tif":
                case "tiff":
                    _encoderType = ImageEncoderType.TiffBitmap;
                    break;
                case "gif":
                    _encoderType = ImageEncoderType.GifBitmap;
                    break;
                case "wdp":
                    _encoderType = ImageEncoderType.WmpBitmap;
                    break;
            }
    }

    #endregion

    #region Private Methods

    private void Notify(string propertyName)
    {
        var handler = PropertyChanged;
        if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region Private Fields

    private bool _textAsGeometry;
    private bool _includeRuntime;
    private bool _generateImage;
    private bool _generalWpf;
    private bool _saveXaml;
    private bool _saveZaml;
    private bool _customXamlWriter;

    private ImageEncoderType _encoderType;

    #endregion

    #region Constructors and Destructor

    public ConverterOptions()
    {
        _textAsGeometry = true;
        _includeRuntime = false;
        _generateImage = false;
        _generalWpf = true;
        _saveXaml = true;
        _saveZaml = false;
        _customXamlWriter = true;
        Message = string.Empty;
        _encoderType = ImageEncoderType.PngBitmap;
    }

    public ConverterOptions(ConverterOptions source)
    {
        if (source == null) throw new ArgumentNullException("source");

        _textAsGeometry = source._textAsGeometry;
        _includeRuntime = source._includeRuntime;
        _generateImage = source._generateImage;
        _generalWpf = source._generalWpf;
        _saveXaml = source._saveXaml;
        _saveZaml = source._saveZaml;
        _customXamlWriter = source._customXamlWriter;
        _encoderType = source._encoderType;
        Message = source.Message;
    }

    #endregion

    #region Public Properties

    public bool IsValid
    {
        get
        {
            Message = string.Empty;

            if (!_generateImage && !_generalWpf)
            {
                Message = "No output target (XAML or Image) is selected.";

                return false;
            }

            if (_generalWpf)
                if (!_saveXaml && !_saveZaml)
                {
                    Message = "The XAML output target is selected but no file format is specified.";

                    return false;
                }

            return true;
        }
    }

    public string Message { get; private set; }

    public bool TextAsGeometry
    {
        get => _textAsGeometry;
        set
        {
            if (_textAsGeometry != value)
            {
                Notify("TextAsGeometry");

                _textAsGeometry = value;
            }
        }
    }

    public bool IncludeRuntime
    {
        get => _includeRuntime;
        set
        {
            if (_includeRuntime != value)
            {
                Notify("IncludeRuntime");

                _includeRuntime = value;
            }
        }
    }

    public bool GenerateImage
    {
        get => _generateImage;
        set
        {
            if (_generateImage != value)
            {
                Notify("GenerateImage");

                _generateImage = value;
            }
        }
    }

    public bool GeneralWpf
    {
        get => _generalWpf;
        set
        {
            if (_generalWpf != value)
            {
                Notify("GeneralWpf");

                _generalWpf = value;
            }
        }
    }

    public bool SaveXaml
    {
        get => _saveXaml;
        set
        {
            if (_saveXaml != value)
            {
                Notify("SaveXaml");

                _saveXaml = value;
            }
        }
    }

    public bool SaveZaml
    {
        get => _saveZaml;
        set
        {
            if (_saveZaml != value)
            {
                Notify("SaveZaml");

                _saveZaml = value;
            }
        }
    }

    public bool UseCustomXamlWriter
    {
        get => _customXamlWriter;
        set
        {
            if (_customXamlWriter != value)
            {
                Notify("UseCustomXamlWriter");

                _customXamlWriter = value;
            }
        }
    }

    public ImageEncoderType EncoderType
    {
        get => _encoderType;
        set
        {
            if (_encoderType != value)
            {
                Notify("EncoderType");

                _encoderType = value;
            }
        }
    }

    #endregion

    #region ICloneable Members

    public ConverterOptions Clone()
    {
        var options = new ConverterOptions(this);
        if (Message != null) options.Message = new string(Message.ToCharArray());

        return options;
    }

    object ICloneable.Clone()
    {
        return Clone();
    }

    #endregion
}