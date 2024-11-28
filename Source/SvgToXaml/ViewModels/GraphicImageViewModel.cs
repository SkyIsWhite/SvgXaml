using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SvgToXaml.ViewModels;

internal class GraphicImageViewModel : ImageBaseViewModel
{
    public GraphicImageViewModel(string filepath) : base(filepath)
    {
    }

    public static string SupportedFormats => "*.jpg|*.jpeg|*.png|*.bmp|*.tiff|*.gif";

    public override bool HasXaml => false;
    public override bool HasSvg => false;

    protected override ImageSource GetImageSource()
    {
        return new BitmapImage(new Uri(Filepath, UriKind.RelativeOrAbsolute));
    }

    protected override string GetSvgDesignInfo()
    {
        if (PreviewSource is BitmapImage)
        {
            var bi = (BitmapImage)PreviewSource;
            return $"{bi.PixelWidth}x{bi.PixelHeight}";
        }

        return null;
    }
}