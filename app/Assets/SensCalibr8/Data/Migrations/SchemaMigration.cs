using System;
using System.Security.Cryptography;
using System.Text;

namespace SensCalibr8.Data.Migrations
{
    public sealed class SchemaMigration
    {
        public SchemaMigration(int version, string name, string sql)
        {
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Migration name is required.", nameof(name));
            if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("Migration SQL is required.", nameof(sql));
            Version = version;
            Name = name;
            Sql = sql;
            Checksum = ComputeChecksum(sql);
        }

        public int Version { get; }
        public string Name { get; }
        public string Sql { get; }
        public string Checksum { get; }

        private static string ComputeChecksum(string value)
        {
            using SHA256 algorithm = SHA256.Create();
            byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
