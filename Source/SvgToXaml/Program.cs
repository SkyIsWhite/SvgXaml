using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace SvgToXaml;

internal static class Program
{
    private static readonly Dictionary<string, Assembly> LoadedAsmsCache =
        new(StringComparer.InvariantCultureIgnoreCase);

    [STAThread]
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;

        var exitCode = 0;
        //normale WPF-Applikationslogik
        var app = new App();
        app.InitializeComponent();
        app.Run();
        return exitCode;
    }

    private static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
    {
        Assembly cachedAsm;
        if (LoadedAsmsCache.TryGetValue(args.Name, out cachedAsm))
            return cachedAsm;

        var executingAssembly = Assembly.GetExecutingAssembly();
        var assemblyName = new AssemblyName(args.Name);

        var path = assemblyName.Name + ".dll";
        if (assemblyName.CultureInfo != null && assemblyName.CultureInfo.Equals(CultureInfo.InvariantCulture) == false)
            path = $@"{assemblyName.CultureInfo}\{path}";

        using (var stream = executingAssembly.GetManifestResourceStream(path))
        {
            if (stream == null)
                return null;

            var assemblyRawBytes = new byte[stream.Length];
            stream.Read(assemblyRawBytes, 0, assemblyRawBytes.Length);
            var loadedAsm = Assembly.Load(assemblyRawBytes);
            LoadedAsmsCache.Add(args.Name, loadedAsm);
            return loadedAsm;
        }
    }
}