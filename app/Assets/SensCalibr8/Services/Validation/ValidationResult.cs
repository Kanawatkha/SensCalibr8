using System;

namespace SensCalibr8.Services.Validation
{
    public sealed class ValidationResult<T>
    {
        private ValidationResult(bool isValid, T value, string errorCode)
        { IsValid = isValid; Value = value; ErrorCode = errorCode; }
        public bool IsValid { get; }
        public T Value { get; }
        public string ErrorCode { get; }
        public static ValidationResult<T> Valid(T value) => new ValidationResult<T>(true, value, null);
        public static ValidationResult<T> Invalid(string errorCode) => new ValidationResult<T>(false, default, !string.IsNullOrWhiteSpace(errorCode) ? errorCode : throw new ArgumentException("Error code is required.", nameof(errorCode)));
    }
}
