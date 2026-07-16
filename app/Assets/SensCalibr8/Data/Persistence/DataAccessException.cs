using System;

namespace SensCalibr8.Data.Persistence
{
    public enum DataFailureKind
    {
        Unavailable,
        ConstraintViolation,
        IntegrityViolation,
        Unexpected
    }

    public enum DataRecoveryAction
    {
        Retry,
        PreserveInMemorySession,
        ReinitializeFromAcceptedConfiguration,
        ContactSupport
    }

    public sealed class DataAccessException : Exception
    {
        public DataAccessException(string operation, Exception innerException)
            : this(operation, Classify(innerException), innerException)
        {
        }

        public DataAccessException(string operation, DataFailureKind failureKind, Exception innerException)
            : base("Database operation failed: " + operation, innerException)
        {
            Operation = Require(operation, nameof(operation));
            FailureKind = failureKind;
            RecoveryAction = SelectRecovery(failureKind);
        }

        public string Operation { get; }
        public DataFailureKind FailureKind { get; }
        public DataRecoveryAction RecoveryAction { get; }

        private static string Require(string value, string field) => !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException(field + " is required.", field);

        private static DataFailureKind Classify(Exception exception)
        {
            string message = exception?.ToString() ?? string.Empty;
            if (message.IndexOf("constraint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("unique", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("foreign key", StringComparison.OrdinalIgnoreCase) >= 0)
                return DataFailureKind.ConstraintViolation;
            if (message.IndexOf("checksum", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("immutable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("foreign-key enforcement", StringComparison.OrdinalIgnoreCase) >= 0)
                return DataFailureKind.IntegrityViolation;
            if (message.IndexOf("open", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("locked", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("native", StringComparison.OrdinalIgnoreCase) >= 0)
                return DataFailureKind.Unavailable;
            return DataFailureKind.Unexpected;
        }

        private static DataRecoveryAction SelectRecovery(DataFailureKind failureKind)
        {
            switch (failureKind)
            {
                case DataFailureKind.ConstraintViolation: return DataRecoveryAction.PreserveInMemorySession;
                case DataFailureKind.IntegrityViolation: return DataRecoveryAction.ReinitializeFromAcceptedConfiguration;
                case DataFailureKind.Unavailable: return DataRecoveryAction.Retry;
                default: return DataRecoveryAction.ContactSupport;
            }
        }
    }
}
