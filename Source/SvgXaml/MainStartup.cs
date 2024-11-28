using System;
using System.Diagnostics;
using System.IO;

namespace SharpVectors.Converters;

internal static class MainStartup
{
    #region Main Method

    [STAThread]
    private static int Main(string[] args)
    {
        // 1. Get a pointer to the foreground window.  The idea here is that                
        // If the user is starting our application from an existing console                
        // shell, that shell will be the uppermost window.  We'll get it                
        // and attach to it.                
        // Uses this idea from, Jeffrey Knight, since it fits our model instead
        // of the recommended ATTACH_PARENT_PROCESS (DWORD)-1 parameter
        var startedInConsole = false;
        var ptr = ConverterWindowsAPI.GetForegroundWindow();
        var processId = -1;
        ConverterWindowsAPI.GetWindowThreadProcessId(ptr, out processId);
        var process = Process.GetProcessById(processId);

        startedInConsole = process != null && string.Equals(
            process.ProcessName, "cmd", StringComparison.OrdinalIgnoreCase);

        // 2. Parse the command-line options to determine the requested task...
        var commandLines = new ConverterCommandLines(args);
        var parseSuccess = commandLines.Parse(startedInConsole);

        var uiOption = commandLines.Ui;
        var isQuiet = uiOption == ConverterUIOption.None;
        // 3. If the parsing is successful...
        if (parseSuccess)
        {
            // A test for possible file drag/drop on application icon
            var sourceCount = 0;
            if (commandLines != null && !commandLines.IsEmpty)
            {
                var sourceFiles = commandLines.SourceFiles;
                if (sourceFiles != null)
                {
                    sourceCount = sourceFiles.Count;
                }
                else
                {
                    var sourceFile = commandLines.SourceFile;
                    if (!string.IsNullOrWhiteSpace(sourceFile) &&
                        File.Exists(sourceFile))
                        sourceCount = 1;
                }
            }

            // For the console or quiet mode...
            if (startedInConsole)
            {
                // If explicitly asked for a Windows GUI...
                if (uiOption == ConverterUIOption.Windows)
                {
                    if (args != null && args.Length != 0 &&
                        args.Length == sourceCount)
                        // if it passes our simple drag/drop test, show
                        // the minimal window for quick conversion of files...
                        return RunWindows(commandLines, false);

                    //...otherwise, display the main window.
                    return RunWindows(commandLines, true);
                }

                var exitCode = RunConsole(commandLines,
                    isQuiet, startedInConsole, process);

                // Exit the application...
                ConverterWindowsAPI.ExitProcess((uint)exitCode);

                return exitCode;
            }

            if (isQuiet || uiOption == ConverterUIOption.Console)
            {
                var exitCode = RunConsole(commandLines,
                    isQuiet, startedInConsole, process);

                // Exit the application...
                ConverterWindowsAPI.ExitProcess((uint)exitCode);

                return exitCode;
            }

            //...for the GUI Windows mode...
            if (args != null && args.Length != 0 &&
                args.Length == sourceCount)
                // if it passes our simple drag/drop test, show
                // the minimal window for quick conversion of files...
                return RunWindows(commandLines, false);

            //...otherwise, display the main window.
            return RunWindows(commandLines, true);
        }

        //... else if the parsing failed...
        if (commandLines != null) commandLines.ShowHelp = true;

        if (startedInConsole ||
            (commandLines != null && uiOption == ConverterUIOption.Console))
        {
            var exitCode = RunConsoleHelp(commandLines,
                isQuiet, startedInConsole, process);

            // Exit the application...
            ConverterWindowsAPI.ExitProcess((uint)exitCode);

            return exitCode;
        }

        return RunWindows(commandLines, true);
    }

    #endregion

    #region Other Methods

    private static int RunConsole(ConverterCommandLines commandLines,
        bool isQuiet, bool startedInConsole, Process process)
    {
        var theApp = new ConsoleApplication(process);
        theApp.CommandLines = commandLines;
        theApp.InitializeComponent(startedInConsole, isQuiet);

        return theApp.Run();
    }

    private static int RunConsoleHelp(ConverterCommandLines commandLines,
        bool isQuiet, bool startedInConsole, Process process)
    {
        var theApp = new ConsoleApplication(process);
        theApp.CommandLines = commandLines;
        theApp.InitializeComponent(startedInConsole, isQuiet);

        return theApp.Help();
    }

    private static int RunWindows(ConverterCommandLines commandLines,
        bool isMainWindow)
    {
        var theApp = new MainApplication();
        theApp.CommandLines = commandLines;
        theApp.InitializeComponent(isMainWindow);

        return theApp.Run();
    }

    #endregion
}