using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace SensCalibr8.Calibration
{
    public sealed class AppendOnlyJsonLinesWriter : IDisposable
    {
        private readonly ConcurrentQueue<string> pendingLines = new ConcurrentQueue<string>();
        private readonly AutoResetEvent pendingSignal = new AutoResetEvent(false);
        private readonly FileStream stream;
        private readonly StreamWriter writer;
        private readonly Thread writerThread;
        private volatile bool stopRequested;
        private bool disposed;
        private Exception backgroundFailure;

        public AppendOnlyJsonLinesWriter(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("An append-only artifact path is required.", "path");
            }

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("The artifact path must include a directory.", "path");
            }

            Directory.CreateDirectory(directory);
            stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            writer = new StreamWriter(stream, new UTF8Encoding(false));
            writerThread = new Thread(WritePendingLines);
            writerThread.IsBackground = true;
            writerThread.Name = "SensCalibr8CalibrationArtifactWriter";
            writerThread.Start();
        }

        public void Append(string jsonLine)
        {
            if (jsonLine == null)
            {
                throw new ArgumentNullException("jsonLine");
            }

            ThrowIfUnavailable();
            pendingLines.Enqueue(jsonLine);
            pendingSignal.Set();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            stopRequested = true;
            pendingSignal.Set();
            writerThread.Join();
            pendingSignal.Dispose();
            disposed = true;
            ThrowIfBackgroundFailed();
        }

        private void WritePendingLines()
        {
            try
            {
                while (!stopRequested || !pendingLines.IsEmpty)
                {
                    string line;
                    bool wroteLine = false;
                    while (pendingLines.TryDequeue(out line))
                    {
                        writer.WriteLine(line);
                        wroteLine = true;
                    }

                    if (wroteLine)
                    {
                        writer.Flush();
                    }

                    if (!stopRequested)
                    {
                        pendingSignal.WaitOne();
                    }
                }

                writer.Flush();
                stream.Flush(true);
            }
            catch (Exception exception)
            {
                backgroundFailure = exception;
            }
            finally
            {
                writer.Dispose();
                stream.Dispose();
            }
        }

        private void ThrowIfUnavailable()
        {
            if (disposed || stopRequested)
            {
                throw new ObjectDisposedException(typeof(AppendOnlyJsonLinesWriter).Name);
            }

            ThrowIfBackgroundFailed();
        }

        private void ThrowIfBackgroundFailed()
        {
            if (backgroundFailure != null)
            {
                throw new IOException("The calibration artifact writer failed.", backgroundFailure);
            }
        }
    }
}
