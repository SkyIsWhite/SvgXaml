using System.IO;
using System.Windows;

namespace SvgConverter;

public class ConvertedSvgData
{
    private DependencyObject _convertedObj;
    private string _objectName;
    private string _svg;
    private string _xaml;

    public string Filepath { get; set; }

    public string Xaml
    {
        get => _xaml ?? (_xaml = ConverterLogic.SvgObjectToXaml(ConvertedObj, false, _objectName, false));
        set => _xaml = value;
    }

    public string Svg
    {
        get => _svg ?? (_svg = File.ReadAllText(Filepath));
        set => _svg = value;
    }

    public DependencyObject ConvertedObj
    {
        get
        {
            if (_convertedObj == null)
                _convertedObj =
                    ConverterLogic.ConvertSvgToObject(Filepath, ResultMode.DrawingImage, null, out _objectName,
                        new ResKeyInfo()) as DependencyObject;
            return _convertedObj;
        }
        set => _convertedObj = value;
    }
}