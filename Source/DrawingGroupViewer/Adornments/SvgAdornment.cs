using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
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
        public int PreviewSideLength { get; set; } = 256;

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
                var bitmap = ConvertToBitmapImage(drawingGroup, PreviewSideLength);
                int width = bitmap.PixelWidth;
                int height = bitmap.PixelHeight;
                bitmap.Freeze();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                ToolTip = $"Width: {width}\nHeight: {height}";
                Source = bitmap;
                UpdateAdornmentLocation(bitmap.Width, bitmap.Height);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }
        }

        private BitmapImage ConvertToBitmapImage(DrawingGroup drawingGroup, double targetSize)
        {
            // 获取 DrawingGroup 的实际大小
            Rect bounds = drawingGroup.Bounds;

            // 如果 DrawingGroup 为空或尺寸为 0，则直接返回一个空的 BitmapImage
            if (bounds.Width == 0 || bounds.Height == 0)
            {
                return new BitmapImage();
            }

            double originalWidth = bounds.Width;
            double originalHeight = bounds.Height;

            // 计算缩放比例，保持宽高比
            double scale;

            // 判断是宽度还是高度作为目标尺寸
            if (originalWidth > originalHeight)
            {
                // 使用宽度为基准，计算缩放比例
                scale = targetSize / originalWidth;
            }
            else
            {
                // 使用高度为基准，计算缩放比例
                scale = targetSize / originalHeight;
            }

            // 计算目标宽高，保持原始比例
            double targetWidth = originalWidth * scale;
            double targetHeight = originalHeight * scale;

            // 使用 ScaleTransform 缩放 DrawingGroup
            ScaleTransform scaleTransform = new ScaleTransform(scale, scale);

            // 使用 DrawingVisual 渲染 DrawingGroup
            DrawingGroup scaledDrawingGroup = new DrawingGroup();
            using (DrawingContext dc = scaledDrawingGroup.Open())
            {
                dc.PushTransform(scaleTransform);
                dc.DrawDrawing(drawingGroup);
                dc.Pop();
            }

            // 计算渲染后的尺寸，以确保 RenderTargetBitmap 足够大
            int renderWidth = (int)targetWidth;
            int renderHeight = (int)targetHeight;

            // 创建一个 RenderTargetBitmap 来渲染背景和内容
            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                renderWidth, renderHeight, 96, 96, PixelFormats.Pbgra32);

            // 创建一个 DrawingVisual 来绘制背景和 DrawingGroup 内容
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // 绘制格子背景
                DrawGridBackground(drawingContext, renderWidth, renderHeight);

                // 绘制 DrawingGroup 内容
                drawingContext.DrawDrawing(scaledDrawingGroup);
            }

            // 渲染到 RenderTargetBitmap
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

        private void DrawGridBackground(DrawingContext drawingContext, int width, int height)
        {
            // 设置格子的颜色和大小
            Brush lightGray = new SolidColorBrush(Color.FromArgb(128, 200, 200, 200)); // 浅灰色
            Brush darkGreen = new SolidColorBrush(Color.FromArgb(128, 0, 128, 0)); // 深绿色

            double cellSize = 20; // 格子的大小

            // 绘制格子背景
            for (double y = 0; y < height; y += cellSize)
            {
                for (double x = 0; x < width; x += cellSize)
                {
                    Brush brush = (int)((x + y) / cellSize) % 2 == 0 ? lightGray : darkGreen;
                    drawingContext.DrawRectangle(brush, null, new Rect(x, y, cellSize, cellSize));
                }
            }
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