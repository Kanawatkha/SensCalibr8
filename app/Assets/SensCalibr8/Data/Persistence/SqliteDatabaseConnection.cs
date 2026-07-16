using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SensCalibr8.Data.Persistence
{
    public sealed class SqliteDatabaseConnection : IDisposable
    {
        private IntPtr database;

        internal SqliteDatabaseConnection(string databasePath) { DatabasePath = databasePath; }

        public string DatabasePath { get; }
        public bool IsOpen => database != IntPtr.Zero;

        internal void Open()
        {
            if (IsOpen) throw new InvalidOperationException("SQLite connection is already open.");
            if (Native.sqlite3_initialize() != Native.Ok) throw new InvalidOperationException("SQLite native runtime initialization failed.");
            int result = Native.sqlite3_open_v2(Utf8(DatabasePath), out database, Native.OpenReadWrite | Native.OpenCreate | Native.OpenFullMutex, IntPtr.Zero);
            if (result != Native.Ok)
            {
                string message = ErrorMessage();
                Dispose();
                throw new InvalidOperationException("SQLite open failed: " + message);
            }
            Native.sqlite3_busy_timeout(database, 5000);
        }

        internal void ExecuteScript(string sql)
        {
            RequireOpen();
            if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL is required.", nameof(sql));
            IntPtr error;
            int result = Native.sqlite3_exec(database, Utf8(sql), IntPtr.Zero, IntPtr.Zero, out error);
            if (result == Native.Ok) return;
            string message = error == IntPtr.Zero ? ErrorMessage() : Marshal.PtrToStringAnsi(error);
            if (error != IntPtr.Zero) Native.sqlite3_free(error);
            throw new InvalidOperationException("SQLite script failed: " + message);
        }

        internal int Execute(string sql, IReadOnlyDictionary<string, object> parameters = null)
        {
            IntPtr statement = Prepare(sql);
            try
            {
                Bind(statement, parameters);
                int result = Native.sqlite3_step(statement);
                if (result != Native.Done) throw new InvalidOperationException("SQLite command failed: " + ErrorMessage());
                return Native.sqlite3_changes(database);
            }
            finally { Native.sqlite3_finalize(statement); }
        }

        internal object Scalar(string sql, IReadOnlyDictionary<string, object> parameters = null)
        {
            IReadOnlyList<IReadOnlyDictionary<string, object>> rows = Query(sql, parameters);
            if (rows.Count == 0) return null;
            foreach (object value in rows[0].Values) return value;
            return null;
        }

        internal IReadOnlyList<IReadOnlyDictionary<string, object>> Query(string sql, IReadOnlyDictionary<string, object> parameters = null)
        {
            IntPtr statement = Prepare(sql);
            try
            {
                Bind(statement, parameters);
                var rows = new List<IReadOnlyDictionary<string, object>>();
                while (true)
                {
                    int result = Native.sqlite3_step(statement);
                    if (result == Native.Done) return rows;
                    if (result != Native.Row) throw new InvalidOperationException("SQLite query failed: " + ErrorMessage());
                    int count = Native.sqlite3_column_count(statement);
                    var row = new Dictionary<string, object>(count, StringComparer.Ordinal);
                    for (int index = 0; index < count; index++)
                        row.Add(ReadText(Native.sqlite3_column_name(statement, index)), ReadColumn(statement, index));
                    rows.Add(row);
                }
            }
            finally { Native.sqlite3_finalize(statement); }
        }

        public void Dispose()
        {
            if (database == IntPtr.Zero) return;
            Native.sqlite3_close_v2(database);
            database = IntPtr.Zero;
        }

        internal SqliteTransaction BeginImmediateTransaction()
        {
            RequireOpen();
            ExecuteScript("BEGIN IMMEDIATE;");
            return new SqliteTransaction(this);
        }

        internal long LastInsertRowId()
        {
            RequireOpen();
            return Native.sqlite3_last_insert_rowid(database);
        }

        private IntPtr Prepare(string sql)
        {
            RequireOpen();
            if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL is required.", nameof(sql));
            IntPtr statement;
            int result = Native.sqlite3_prepare_v2(database, Utf8(sql), -1, out statement, IntPtr.Zero);
            if (result != Native.Ok) throw new InvalidOperationException("SQLite prepare failed: " + ErrorMessage());
            return statement;
        }

        private void Bind(IntPtr statement, IReadOnlyDictionary<string, object> parameters)
        {
            if (parameters == null) return;
            foreach (KeyValuePair<string, object> parameter in parameters)
            {
                int index = Native.sqlite3_bind_parameter_index(statement, Utf8(parameter.Key));
                if (index == 0) throw new InvalidOperationException("SQLite parameter was not found: " + parameter.Key);
                object value = parameter.Value;
                int result;
                if (value == null || value == DBNull.Value) result = Native.sqlite3_bind_null(statement, index);
                else if (value is bool boolean) result = Native.sqlite3_bind_int64(statement, index, boolean ? 1L : 0L);
                else if (value is byte || value is short || value is int || value is long) result = Native.sqlite3_bind_int64(statement, index, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                else if (value is float || value is double || value is decimal) result = Native.sqlite3_bind_double(statement, index, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                else
                {
                    byte[] text = Encoding.UTF8.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture));
                    result = Native.sqlite3_bind_text(statement, index, text, text.Length, new IntPtr(-1));
                }
                if (result != Native.Ok) throw new InvalidOperationException("SQLite bind failed: " + ErrorMessage());
            }
        }

        private static object ReadColumn(IntPtr statement, int index)
        {
            switch (Native.sqlite3_column_type(statement, index))
            {
                case Native.Integer: return Native.sqlite3_column_int64(statement, index);
                case Native.Float: return Native.sqlite3_column_double(statement, index);
                case Native.Text: return ReadText(Native.sqlite3_column_text(statement, index), Native.sqlite3_column_bytes(statement, index));
                case Native.Null: return null;
                default: return ReadText(Native.sqlite3_column_text(statement, index), Native.sqlite3_column_bytes(statement, index));
            }
        }

        private string ErrorMessage() => database == IntPtr.Zero ? "unknown error" : Marshal.PtrToStringAnsi(Native.sqlite3_errmsg(database));
        private void RequireOpen() { if (!IsOpen) throw new ObjectDisposedException(nameof(SqliteDatabaseConnection)); }
        private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value + "\0");
        private static string ReadText(IntPtr value) => value == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(value);
        private static string ReadText(IntPtr value, int length)
        {
            if (value == IntPtr.Zero || length == 0) return string.Empty;
            byte[] bytes = new byte[length];
            Marshal.Copy(value, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        private static class Native
        {
            public const int Ok = 0, Row = 100, Done = 101;
            public const int Integer = 1, Float = 2, Text = 3, Null = 5;
            public const int OpenReadWrite = 0x00000002, OpenCreate = 0x00000004, OpenFullMutex = 0x00010000;

            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_initialize();
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_open_v2(byte[] filename, out IntPtr database, int flags, IntPtr vfs);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_close_v2(IntPtr database);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_busy_timeout(IntPtr database, int milliseconds);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr sqlite3_errmsg(IntPtr database);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_exec(IntPtr database, byte[] sql, IntPtr callback, IntPtr argument, out IntPtr errorMessage);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern void sqlite3_free(IntPtr value);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_prepare_v2(IntPtr database, byte[] sql, int bytes, out IntPtr statement, IntPtr tail);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_step(IntPtr statement);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_finalize(IntPtr statement);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_changes(IntPtr database);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern long sqlite3_last_insert_rowid(IntPtr database);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_bind_parameter_index(IntPtr statement, byte[] name);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_bind_null(IntPtr statement, int index);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_bind_int64(IntPtr statement, int index, long value);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_bind_double(IntPtr statement, int index, double value);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_bind_text(IntPtr statement, int index, byte[] value, int bytes, IntPtr destructor);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_column_count(IntPtr statement);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr sqlite3_column_name(IntPtr statement, int index);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_column_type(IntPtr statement, int index);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern long sqlite3_column_int64(IntPtr statement, int index);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern double sqlite3_column_double(IntPtr statement, int index);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr sqlite3_column_text(IntPtr statement, int index);
            [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)] public static extern int sqlite3_column_bytes(IntPtr statement, int index);
        }
    }
}
