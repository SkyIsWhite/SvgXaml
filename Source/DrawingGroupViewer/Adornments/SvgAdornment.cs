using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;

namespace DrawingGroupViewer.Adornments
{
    public enum ScalingMode
    {
        ScaleDown,
        ScaleUp,
    }

    internal class SvgAdornment : Image
    {
        private readonly ITextView _view;

        public SvgAdornment(IWpfTextView view)
        {
            _view = view;

            Visibility = Visibility.Hidden;

            IAdornmentLayer adornmentLayer = view.GetAdornmentLayer(AdornmentLayer.LayerName);

            if (adornmentLayer.IsEmpty)
            {
                adornmentLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, this, null);
            }

            _view.TextBuffer.PostChanged += OnTextBufferChanged;
            _view.Closed += OnTextViewClosed;
            _view.ViewportHeightChanged += SetAdornmentLocation;
            _view.ViewportWidthChanged += SetAdornmentLocation;

            GenerateImageAsync().FireAndForget();
        }

        private void OnTextBufferChanged(object sender, EventArgs e)
        {
            var lastVersion = _view.TextBuffer.CurrentSnapshot.Version.VersionNumber;

            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await Task.Delay(500);

                if (_view.TextBuffer.CurrentSnapshot.Version.VersionNumber == lastVersion)
                {
                    await GenerateImageAsync();
                }
            });
        }

        private void OnTextViewClosed(object sender, EventArgs e)
        {
            _view.Closed -= OnTextViewClosed;
            _view.TextBuffer.PostChanged -= OnTextBufferChanged;
            _view.ViewportHeightChanged -= SetAdornmentLocation;
            _view.ViewportWidthChanged -= SetAdornmentLocation;
        }

        private void OnDocumentSaved(object sender, TextDocumentFileActionEventArgs e)
        {
            GenerateImageAsync().FireAndForget();
        }

        /// <summary>
        /// Length of the preview side that's being scaled to.
        /// If <see cref="ScalingMode"/> is ScaleUp, that's the minimal side length.
        /// If <see cref="ScalingMode"/> is ScaleDown, that's the maximal side length.
        /// </summary>
        public int PreviewSideLength { get; set; } = 250;

        /// <summary>
        /// Scale the image up or down?
        /// </summary>
        public ScalingMode ScalingMode { get; set; } = ScalingMode.ScaleUp;

        private async Task GenerateImageAsync()
        {
            await TaskScheduler.Default;

            try
            {
                if (!TryGetBufferAsXmlDocument(out XmlDocument xml))
                {
                    Source = null;
                    return;
                }
                string xmlContent = xml.OuterXml;
                var drawingGroup = XamlReader.Parse(xmlContent) as DrawingGroup;

                if (drawingGroup == null)
                {
                    return;
                }

                Size size = CalculateDimensions(new Size(drawingGroup.Bounds.Width, drawingGroup.Bounds.Height));

                var bitmap = ConvertToBitmapImage(drawingGroup, size);

                bitmap.Freeze();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                ToolTip = $"Width: {size.Width}\nHeight: {size.Height}";
                Source = bitmap;
                UpdateAdornmentLocation(bitmap.Width, bitmap.Height);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }
        }

        /// <summary>
        /// 将 DrawingGroup 转换为 BitmapImage
        /// </summary>
        private BitmapImage ConvertToBitmapImage(DrawingGroup drawingGroup, Size size)
        {
            // 渲染到 RenderTargetBitmap
            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                (int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);

            // 获取 DrawingGroup 的实际大小
            Rect bounds = drawingGroup.Bounds;
            double originalWidth = bounds.Width;
            double originalHeight = bounds.Height;

            // 计算缩放比例
            double scaleX = size.Width / originalWidth;
            double scaleY = size.Height / originalHeight;

            // 使用 ScaleTransform 缩放 DrawingGroup
            ScaleTransform scaleTransform = new ScaleTransform(scaleX, scaleY);

            // 使用 DrawingVisual 渲染 DrawingGroup
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.PushTransform(scaleTransform);
                drawingContext.DrawDrawing(drawingGroup);
                drawingContext.Pop();
            }

            renderBitmap.Render(drawingVisual);

            // 将 RenderTargetBitmap 转换为 BitmapImage
            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                // 使用 PngBitmapEncoder 将 RenderTargetBitmap 编码到内存流
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                encoder.Save(memoryStream);

                // 将内存流加载到 BitmapImage
                memoryStream.Seek(0, SeekOrigin.Begin);
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // 冻结以提高性能
            }

            return bitmapImage;
        }

        private bool TryGetBufferAsXmlDocument(out XmlDocument document)
        {
            document = new XmlDocument();

            try
            {
                var xml = _view.TextBuffer.CurrentSnapshot.GetText();
                document.LoadXml(xml);

                return true;
            }
            catch (XmlException)
            {
                return false;
            }
        }

        private Size CalculateDimensions(Size currentSize)
        {
            var sourceWidth = currentSize.Width;
            var sourceHeight = currentSize.Height;

            var widthPercent = PreviewSideLength / sourceWidth;
            var heightPercent = PreviewSideLength / sourceHeight;

            var percent = ScalingMode == ScalingMode.ScaleUp ?
                Math.Max(heightPercent, widthPercent) :
                Math.Min(heightPercent, widthPercent);

            var destWidth = (int)(sourceWidth * percent);
            var destHeight = (int)(sourceHeight * percent);

            return new Size(destWidth, destHeight);
        }

        private void SetAdornmentLocation(object sender, EventArgs e)
        {
            UpdateAdornmentLocation(ActualWidth, ActualHeight);
        }

        private void UpdateAdornmentLocation(double width, double height)
        {
            Canvas.SetLeft(this, _view.ViewportRight - width - 20);
            Canvas.SetTop(this, _view.ViewportBottom - height - 20);
            Visibility = Visibility.Visible;
        }
    }
}