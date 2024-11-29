using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace SharpVectors.Converters
{
    /// <summary>
    /// DetailWindow.xaml 的交互逻辑
    /// </summary>
    public partial class DetailWindow : Window
    {
        public DetailWindow()
        {
            InitializeComponent();
        }

        public bool AllowContinue { get; private set; } = false;
        private bool _allowClose = false;

        private void OnBtnContinueClick(object sender, RoutedEventArgs e)
        {
            AllowContinue = true;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = !_allowClose;
        }

        public void SetSource(int index, int total, string svgFilePath)
        {
            _allowClose = index == total;
            AllowContinue = false;
            string xamlFilePath = svgFilePath.Replace(".svg", ".xaml");
            TbTitle.Text = $"File List Conversion ({index}/{total})";
            TbSvgFilePath.Text = Path.GetFileName(svgFilePath);
            TbXamlFilePath.Text = Path.GetFileName(xamlFilePath);
            ImageSvg.Source = new Uri(svgFilePath);
            var drawingGroup = LoadDrawingGroupByPath(xamlFilePath);
            ImageXaml.Source = new DrawingImage(drawingGroup);
        }

        private DrawingGroup LoadDrawingGroupByPath(string path)
        {
            FileStream info = new FileStream(path, FileMode.Open);
            XamlReader reader = new XamlReader();
            return reader.LoadAsync(info) as DrawingGroup;
        }
    }
}