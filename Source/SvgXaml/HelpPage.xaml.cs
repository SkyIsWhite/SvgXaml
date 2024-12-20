﻿using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using SharpVectors.Converters.Properties;

namespace SharpVectors.Converters;

/// <summary>
///     Interaction logic for HelpPage.xaml
/// </summary>
public partial class HelpPage : Page
{
    private bool _isInitializing;

    public HelpPage()
    {
        InitializeComponent();

        // Reset the dimensions...
        Width = double.NaN;
        Height = double.NaN;

        Loaded += OnHelpPageLoaded;
        Unloaded += OnHelpPageUnloaded;
        SizeChanged += OnHelpPageSizeChanged;

        var zoomProperty =
            DependencyPropertyDescriptor.FromProperty(FlowDocumentReader.ZoomProperty, typeof(FlowDocumentReader));

        zoomProperty.AddValueChanged(helpViewer, OnZoomChanged);
    }

    private void OnHelpPageLoaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;

        if (helpViewer != null && helpViewer.IsInitialized) helpViewer.Zoom = Settings.Default.HelpViewerZoom;

        try
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                "SharpVectors.Converters.ConverterHelp.xaml");
            if (stream == null) return;
            var flowDocument = (FlowDocument)XamlReader.Load(stream);
            helpViewer.Document = flowDocument;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "SVG-WPF Converter",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        _isInitializing = false;
    }

    private void OnHelpPageUnloaded(object sender, RoutedEventArgs e)
    {
        if (helpViewer != null && helpViewer.IsInitialized) Settings.Default.HelpViewerZoom = (float)helpViewer.Zoom;
    }

    private void OnHelpPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (helpViewer != null && helpViewer.IsInitialized)
        {
            //helpViewer.Width = e.NewSize.Width;
        }
    }

    private void OnZoomChanged(object sender, EventArgs e)
    {
        if (_isInitializing) return;

        if (helpViewer != null && helpViewer.IsInitialized) Settings.Default.HelpViewerZoom = (float)helpViewer.Zoom;
    }
}