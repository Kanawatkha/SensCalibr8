using System;
using System.Collections.Generic;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class CalibrationConfigurationRepository
    {
        private readonly RepositoryExecution execution;
        public CalibrationConfigurationRepository(SqliteConnectionFactory connectionFactory, IDataFailureReporter failureReporter = null) { execution = new RepositoryExecution(connectionFactory, failureReporter); }

        public long RequireId(string configVersion)
        {
            if (string.IsNullOrWhiteSpace(configVersion)) throw new ArgumentException("Configuration version is required.", nameof(configVersion));
            return execution.Read("read accepted calibration configuration", connection =>
            {
                object value = connection.Scalar("SELECT id FROM calibration_configs WHERE config_version=@config_version;", new Dictionary<string, object> { ["@config_version"] = configVersion });
                if (value == null) throw new InvalidOperationException("The accepted calibration configuration is not present in this database.");
                return Convert.ToInt64(value);
            });
        }
    }
}
