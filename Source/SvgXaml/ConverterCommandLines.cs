using System;
using System.Collections.Generic;
using System.IO;
using Mono.Options;

namespace SharpVectors.Converters;

public sealed class ConverterCommandLines
{
    #region Constructors and Destructor

    public ConverterCommandLines(string[] args)
    {
        Arguments = args;
        Ui = ConverterUIOption.Unknown;
        BeepOnEnd = false;
        ShowHelp = false;
        Recursive = false;
        IncludeRuntime = false;
        ContinueOnError = true;
        TextAsGeometry = true;
        UseCustomXamlWriter = true;
        SaveXaml = true;
        SaveZaml = false;
    }

    #endregion

    #region Public Methods

    public bool Parse(bool startedInConsole)
    {
        var parser = new OptionSet();
        try
        {
            var sourceSet = new
                Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            DefineOptions(parser, sourceSet);

            using (var writer = new StringWriter())
            {
                BeginOptionsUsage(writer);
                parser.WriteOptionDescriptions(writer);
                EndOptionsUsage(writer);

                Usage = writer.ToString();
            }

            var listExtra = parser.Parse(Arguments);
            var sourceArgs = new List<string>(sourceSet.Keys);

            if (listExtra != null && listExtra.Count != 0) sourceArgs.AddRange(listExtra);

            if (sourceArgs != null && sourceArgs.Count != 0)
            {
                _sources = new List<string>(sourceArgs.Count);

                var sourceFiles = new List<string>();
                var sourceDirs = new List<string>();

                for (var i = 0; i < sourceArgs.Count; i++)
                {
                    var sourcePath = Path.GetFullPath(
                        Environment.ExpandEnvironmentVariables(sourceArgs[i]));

                    if (File.Exists(sourcePath))
                    {
                        // It is a file, but must be SVG to be useful...
                        var fileExt = Path.GetExtension(sourcePath);
                        if (!string.IsNullOrWhiteSpace(fileExt) &&
                            (fileExt.Equals(".svg", StringComparison.OrdinalIgnoreCase) ||
                             fileExt.Equals(".svgz", StringComparison.OrdinalIgnoreCase)))
                            sourceFiles.Add(sourcePath);
                        _sources.Add(sourcePath);
                    }
                    else if (Directory.Exists(sourcePath))
                    {
                        // It is a directory...
                        sourceDirs.Add(sourcePath);
                        _sources.Add(sourcePath);
                    }
                }

                var itemCount = _sources.Count;
                if (itemCount == 1)
                {
                    if (sourceFiles.Count == 1)
                        SourceFile = sourceFiles[0];
                    else if (sourceDirs.Count == 1)
                        SourceDir = sourceDirs[0];
                    else
                        throw new InvalidOperationException(
                            "The input source file is not valid.");
                }
                else if (itemCount > 1)
                {
                    if (sourceFiles.Count > 1)
                        SourceFiles = sourceFiles;
                    else
                        throw new InvalidOperationException(
                            "The input source file is not valid.");
                }
            }

            if (startedInConsole && IsEmpty) return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Private Fields

    private string _image;

    private List<string> _sources;

    #endregion

    #region Public Properties

    public bool IsEmpty
    {
        get
        {
            if (Arguments == null || Arguments.Length == 0) return true;

            return string.IsNullOrWhiteSpace(SourceFile) &&
                   (SourceFiles == null || SourceFiles.Count == 0) &&
                   string.IsNullOrWhiteSpace(SourceDir);
        }
    }

    public string[] Arguments { get; }

    public string OutputDir { get; set; }

    public bool Recursive { get; set; }

    public bool IncludeRuntime { get; set; }

    public bool ContinueOnError { get; set; }

    public bool TextAsGeometry { get; set; }

    public bool UseCustomXamlWriter { get; set; }

    public bool SaveXaml { get; set; }

    public bool SaveZaml { get; set; }

    public bool SaveImage => IsValidImage(_image);

    public bool ShowHelp { get; set; }

    public bool BeepOnEnd { get; set; }

    public string Image
    {
        get => _image;
        set
        {
            if (IsValidImage(value))
                _image = value;
            else
                _image = string.Empty;
        }
    }

    public ConverterUIOption Ui { get; set; }

    public IList<string> Sources => _sources;

    public string SourceFile { get; private set; }

    public string SourceDir { get; private set; }

    public IList<string> SourceFiles { get; private set; }

    public string Usage { get; private set; }

    #endregion

    #region Private Methods

    private void DefineOptions(OptionSet options,
        IDictionary<string, bool> sourceSet)
    {
        options.Add("s|source=", "Specifies the input source files or directory.", delegate(string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) sourceSet[value] = true;
        });
        options.Add("o|output=", "Specifies the output directory.", delegate(string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) OutputDir = value;
        });
        options.Add("r|recursive", "Specifies whether a directory conversion is recursive.", delegate(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                (string.Equals(value, "+", StringComparison.Ordinal)
                 || string.Equals(value, "-", StringComparison.Ordinal)))
                Recursive = string.Equals(value, "+", StringComparison.Ordinal);
        });
        options.Add("t|runtime", "Specifies whether to include runtime library support.", delegate(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                (string.Equals(value, "+", StringComparison.Ordinal)
                 || string.Equals(value, "-", StringComparison.Ordinal)))
                IncludeRuntime = string.Equals(value, "+", StringComparison.Ordinal);
        });
        options.Add("e|onError", "Specifies whether to continue conversion when an error occurs in a file.",
            delegate(string value)
            {
                if (!string.IsNullOrWhiteSpace(value) &&
                    (string.Equals(value, "+", StringComparison.Ordinal)
                     || string.Equals(value, "-", StringComparison.Ordinal)))
                    ContinueOnError = string.Equals(value, "+", StringComparison.Ordinal);
            });
        options.Add("g|textGeometry", "Specifies whether to render texts as path geometry.", delegate(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                (string.Equals(value, "+", StringComparison.Ordinal)
                 || string.Equals(value, "-", StringComparison.Ordinal)))
                TextAsGeometry = string.Equals(value, "+", StringComparison.Ordinal);
        });
        options.Add("c|customXamlWriter", "Specifies whether to use the customized XAML Writer.", delegate(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                (string.Equals(value, "+", StringComparison.Ordinal)
                 || string.Equals(value, "-", StringComparison.Ordinal)))
                UseCustomXamlWriter = string.Equals(value, "+", StringComparison.Ordinal);
        });
        options.Add("x|xaml", "Specifies whether to save in uncompressed XAML format.", delegate(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                (string.Equals(value, "+", StringComparison.Ordinal)
                 || string.Equals(value, "-", StringComparison.Ordinal)))
                SaveXaml = string.Equals(value, "+", StringComparison.Ordinal);
        });
        options.Add("z|zaml", "Specifies whether to save in compressed XAML format.", delegate(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                (string.Equals(value, "+", StringComparison.Ordinal)
                 || string.Equals(value, "-", StringComparison.Ordinal)))
                SaveZaml = string.Equals(value, "+", StringComparison.Ordinal);
        });
        options.Add("i|image=", "Specifies whether to save image and image formats: png, jpeg, tiff, gif, bmp, wdp",
            delegate(string value)
            {
                if (!string.IsNullOrWhiteSpace(value)) Image = value;
            });
        options.Add("u|ui=", "Specifies the user-interface option: none, console or window.", delegate(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                switch (value.ToLower())
                {
                    case "unknown":
                        Ui = ConverterUIOption.Unknown;
                        break;
                    case "null":
                    case "none":
                        Ui = ConverterUIOption.None;
                        break;
                    case "console":
                        Ui = ConverterUIOption.Console;
                        break;
                    case "window":
                    case "windows":
                        Ui = ConverterUIOption.Windows;
                        break;
                }
        });
        options.Add("h|?|help", "Specifies whether to display usage and command-line help.",
            delegate(string value) { ShowHelp = !string.IsNullOrWhiteSpace(value); });
        options.Add("b|beep", "Specifies whether to beep on completion (console only).", delegate(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                (string.Equals(value, "+", StringComparison.Ordinal)
                 || string.Equals(value, "-", StringComparison.Ordinal)))
                BeepOnEnd = string.Equals(value, "+", StringComparison.Ordinal);
        });
    }

    private void BeginOptionsUsage(TextWriter writer)
    {
        writer.WriteLine();
        if (Arguments != null && Arguments.Length != 0)
        {
            writer.WriteLine("Argument Count=" + Arguments.Length);
            foreach (var arg in Arguments) writer.WriteLine(arg);
        }

        writer.WriteLine("Usage: SharpVectors.exe [options]+");
        writer.WriteLine("Options:");
    }

    private void EndOptionsUsage(TextWriter writer)
    {
        writer.WriteLine();
    }

    private static bool IsValidImage(string image)
    {
        if (string.IsNullOrWhiteSpace(image)) return false;
        if (image.Equals("png", StringComparison.OrdinalIgnoreCase)) return true;
        if (image.Equals("jpeg", StringComparison.OrdinalIgnoreCase) ||
            image.Equals("jpg", StringComparison.OrdinalIgnoreCase))
            return true;
        if (image.Equals("tiff", StringComparison.OrdinalIgnoreCase) ||
            image.Equals("tif", StringComparison.OrdinalIgnoreCase))
            return true;
        if (image.Equals("bmp", StringComparison.OrdinalIgnoreCase)) return true;
        if (image.Equals("wdp", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    #endregion
}