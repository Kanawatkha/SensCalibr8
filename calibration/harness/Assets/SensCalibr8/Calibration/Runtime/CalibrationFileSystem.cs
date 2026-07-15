using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SensCalibr8.Calibration
{
    public static class CalibrationArtifactLocation
    {
        public static string CaptureRoot
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SC8",
                    "P0R3");
            }
        }
    }

    public static class CalibrationFileSystem
    {
        public static void WriteNewJson<T>(string path, T value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("The artifact path must include a directory.", "path");
            }

            Directory.CreateDirectory(directory);
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(value, true));
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        public static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] hash = sha256.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        public static IntegrityManifest CreateIntegrityManifest(
            string runDirectory,
            string integrityManifestPath,
            CalibrationRunManifest sourceManifest)
        {
            if (sourceManifest == null)
            {
                throw new ArgumentNullException("sourceManifest");
            }

            List<FileIntegrityRecord> records = new List<FileIntegrityRecord>();
            string[] files = Directory.GetFiles(runDirectory, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);

            for (int index = 0; index < files.Length; index++)
            {
                string file = files[index];
                if (string.Equals(file, integrityManifestPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                FileInfo info = new FileInfo(file);
                records.Add(new FileIntegrityRecord
                {
                    ArtifactId = Path.GetFileNameWithoutExtension(info.Name),
                    RelativePath = info.Name,
                    ByteSize = info.Length,
                    Sha256 = ComputeSha256(file),
                    CreatedUtc = info.CreationTimeUtc.ToString("o"),
                    ProducerVersion = sourceManifest.HarnessVersion,
                    ProducerChecksum = sourceManifest.HarnessChecksum,
                    ProtocolId = sourceManifest.ProtocolId,
                    EnvironmentId = sourceManifest.EnvironmentId,
                    CapturePlanId = sourceManifest.CapturePlanId,
                    ConditionId = sourceManifest.ConditionId,
                    RunId = sourceManifest.RunId,
                    TraceId = sourceManifest.TraceId
                });
            }

            IntegrityManifest manifest = new IntegrityManifest
            {
                ProtocolId = sourceManifest.ProtocolId,
                EnvironmentId = sourceManifest.EnvironmentId,
                CapturePlanId = sourceManifest.CapturePlanId,
                ConditionId = sourceManifest.ConditionId,
                RunId = sourceManifest.RunId,
                TraceId = sourceManifest.TraceId,
                ProducerVersion = sourceManifest.HarnessVersion,
                ProducerChecksum = sourceManifest.HarnessChecksum,
                CreatedUtc = DateTime.UtcNow.ToString("o"),
                Artifacts = records.ToArray()
            };

            WriteNewJson(integrityManifestPath, manifest);
            return manifest;
        }
    }
}
