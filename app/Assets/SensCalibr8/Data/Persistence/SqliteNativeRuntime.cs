using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace SensCalibr8.Data.Persistence
{
    internal static class SqliteNativeRuntime
    {
        private static readonly object Gate = new object();
        private static IntPtr handle;

        public static void EnsureLoaded(string explicitPath)
        {
            lock (Gate)
            {
                if (handle != IntPtr.Zero) return;
                foreach (string candidate in Candidates(explicitPath))
                {
                    if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate)) continue;
                    handle = LoadLibrary(Path.GetFullPath(candidate));
                    if (handle != IntPtr.Zero) return;
                }
                throw new DllNotFoundException("The pinned native sqlite3.dll could not be loaded.");
            }
        }

        private static IEnumerable<string> Candidates(string explicitPath)
        {
            yield return explicitPath;
            yield return Path.Combine(AppContext.BaseDirectory, "sqlite3.dll");
            foreach (string dataDirectory in Directory.GetDirectories(AppContext.BaseDirectory, "*_Data"))
            {
                yield return Path.Combine(dataDirectory, "Plugins", "x86_64", "sqlite3.dll");
                yield return Path.Combine(dataDirectory, "Plugins", "sqlite3.dll");
            }
            yield return Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Plugins", "sqlite3.dll");
            yield return Path.Combine(Directory.GetCurrentDirectory(), "app", "Assets", "Plugins", "sqlite3.dll");
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string path);
    }
}
