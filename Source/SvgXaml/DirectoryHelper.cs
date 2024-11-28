using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security;
using System.Security.Permissions;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace SharpVectors.Converters;

internal class DirectoryHelper
{
    private const int ERROR_FILE_NOT_FOUND = 2;
    private const int ERROR_ACCESS_DENIED = 5;
    private const int ERROR_NO_MORE_FILES = 18;

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern SafeFindHandle FindFirstFile(
        string lpFileName,
        [MarshalAs(UnmanagedType.LPStruct)] [In] [Out]
        WIN32_FIND_DATA lpFindFileData);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool FindNextFile(
        SafeFindHandle hndFindFile,
        [MarshalAs(UnmanagedType.LPStruct)] [In] [Out]
        WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern ErrorModes SetErrorMode(ErrorModes newMode);

    public static void DeleteDirectory(string directoryPath, bool recursive)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            return;
        var dir = new DirectoryInfo(directoryPath);
        if (!dir.Exists)
            return;
        try
        {
            dir.Attributes = FileAttributes.Normal;
            dir.Delete(recursive);
        }
        catch (UnauthorizedAccessException ex)
        {
            foreach (var file in FindFiles(dir, "*.*",
                         recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            dir.Delete(recursive);
        }
    }

    public static string OpenFolderDialog(string sourceDir, string title)
    {
        var selectedDirectory = string.Empty;
        OpenFolderDialog dialog = new();
        dialog.Title = title;
        dialog.Multiselect = false;
        dialog.InitialDirectory = sourceDir;
        var result = dialog.ShowDialog();
        if (result == true) selectedDirectory = dialog.FolderName;
        return selectedDirectory;
    }

    public static IEnumerable<string> FindFiles(
        DirectoryInfo dir,
        string pattern,
        SearchOption searchOption)
    {
        new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Demand();
        if (dir == null)
            throw new ArgumentNullException(nameof(dir));
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));
        var findData = new WIN32_FIND_DATA();
        var directories = new Stack<DirectoryInfo>();
        directories.Push(dir);
        var origErrorMode = SetErrorMode(ErrorModes.FailCriticalErrors);
        try
        {
            while (directories.Count > 0)
            {
                dir = directories.Pop();
                var dirPath = dir.FullName.Trim();
                if (dirPath.Length != 0)
                {
                    var ch = dirPath[dirPath.Length - 1];
                    if (ch != Path.DirectorySeparatorChar && ch != Path.AltDirectorySeparatorChar)
                        dirPath += Path.DirectorySeparatorChar.ToString();
                    var handle = FindFirstFile(dirPath + pattern, findData);
                    if (handle.IsInvalid)
                    {
                        var lastWin32Error = Marshal.GetLastWin32Error();
                        switch (lastWin32Error)
                        {
                            case 2:
                            case 5:
                                continue;
                            default:
                                throw new Win32Exception(lastWin32Error);
                        }
                    }
                    else
                    {
                        try
                        {
                            do
                            {
                                if ((findData.dwFileAttributes & FileAttributes.Directory) == FileAttributes.None)
                                    yield return dirPath + findData.cFileName;
                            } while (FindNextFile(handle, findData));

                            var lastWin32Error = Marshal.GetLastWin32Error();
                            if (lastWin32Error != 18)
                                throw new Win32Exception(lastWin32Error);
                        }
                        finally
                        {
                            handle.Dispose();
                        }

                        if (searchOption == SearchOption.AllDirectories)
                            foreach (var directory in dir.GetDirectories())
                                if ((File.GetAttributes(directory.FullName) & FileAttributes.ReparsePoint) ==
                                    FileAttributes.None)
                                    directories.Push(directory);

                        dirPath = null;
                        handle = null;
                    }
                }
            }
        }
        finally
        {
            var num = (int)SetErrorMode(origErrorMode);
        }
    }

    private sealed class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        private SafeFindHandle()
            : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            return FindClose(handle);
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll")]
        private static extern bool FindClose(IntPtr handle);
    }

    [BestFitMapping(false)]
    [Serializable]
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class WIN32_FIND_DATA
    {
        public FileAttributes dwFileAttributes;
        public FILETIME ftCreationTime;
        public FILETIME ftLastAccessTime;
        public FILETIME ftLastWriteTime;
        public int nFileSizeHigh;
        public int nFileSizeLow;
        public int dwReserved0;
        public int dwReserved1;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    [Flags]
    private enum ErrorModes
    {
        Default = 0,
        FailCriticalErrors = 1,
        NoGpFaultErrorBox = 2,
        NoAlignmentFaultExcept = 4,
        NoOpenFileErrorBox = 32768 // 0x00008000
    }
}