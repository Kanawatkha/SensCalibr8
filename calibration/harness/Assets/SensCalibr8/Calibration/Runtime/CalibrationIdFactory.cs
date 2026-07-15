using System;
using System.Globalization;
using System.Text;

namespace SensCalibr8.Calibration
{
    public static class CalibrationIdFactory
    {
        public static string NormalizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Calibration identifier tokens cannot be blank.", "value");
            }

            StringBuilder builder = new StringBuilder();
            bool previousWasSeparator = false;
            string trimmed = value.Trim().ToLowerInvariant();

            for (int index = 0; index < trimmed.Length; index++)
            {
                char character = trimmed[index];
                bool isAsciiLetter = character >= 'a' && character <= 'z';
                bool isDigit = character >= '0' && character <= '9';

                if (isAsciiLetter || isDigit)
                {
                    builder.Append(character);
                    previousWasSeparator = false;
                }
                else if (!previousWasSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                    previousWasSeparator = true;
                }
            }

            string token = builder.ToString().Trim('-');
            if (token.Length == 0)
            {
                throw new ArgumentException("Calibration identifier tokens must contain an ASCII letter or digit.", "value");
            }

            return token;
        }

        public static string CreateRunId(DateTime utcTimestamp, int repetitionOrdinal)
        {
            if (utcTimestamp.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Run timestamps must be UTC.", "utcTimestamp");
            }

            if (repetitionOrdinal <= 0)
            {
                throw new ArgumentOutOfRangeException("repetitionOrdinal", "Repetition ordinal must be positive.");
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}-r{1}",
                utcTimestamp.ToString("yyyyMMddTHHmmssfffffffZ", CultureInfo.InvariantCulture).ToLowerInvariant(),
                repetitionOrdinal);
        }

        public static string BuildArtifactFileName(
            string protocolId,
            string environmentId,
            string conditionId,
            string runId,
            string artifactType,
            string extension)
        {
            string normalizedExtension = NormalizeToken(extension).Replace("-", string.Empty);
            return string.Format(
                CultureInfo.InvariantCulture,
                "sc8_{0}_{1}_{2}_{3}_{4}.{5}",
                NormalizeToken(protocolId),
                NormalizeToken(environmentId),
                NormalizeToken(conditionId),
                NormalizeToken(runId),
                NormalizeToken(artifactType),
                normalizedExtension);
        }
    }
}
