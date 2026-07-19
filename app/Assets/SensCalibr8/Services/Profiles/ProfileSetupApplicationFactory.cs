using System;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Data.Persistence;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Analysis;
using SensCalibr8.Services.Calculations;
using SensCalibr8.Services.Configuration;

namespace SensCalibr8.Services.Profiles
{
    public static class ProfileSetupApplicationFactory
    {
        public static ProfileSetupApplicationService Open(string databasePath, string repositoryRoot, string nativeLibraryPath, IActiveProfileStore activeProfiles = null)
        {
            if (string.IsNullOrWhiteSpace(databasePath)) throw new ArgumentException("Database path is required.", nameof(databasePath));
            FrozenCalibrationConfiguration calibration = FrozenCalibrationConfigurationLoader.LoadFromRepository(repositoryRoot);
            ResearchConstants constants = ResearchConstantsLoader.LoadFromRepository(repositoryRoot);
            new SqliteDatabaseBootstrapper().Initialize(databasePath, calibration, nativeLibraryPath);
            var connectionFactory = new SqliteConnectionFactory(databasePath, nativeLibraryPath);
            var repository = new ProfileRepository(connectionFactory);
            IProfileClock clock = new SystemProfileClock();
            var lifecycle = new ProfileLifecycleService(repository, activeProfiles ?? new PersistentActiveProfileStore(new ActiveProfileStateRepository(connectionFactory), clock), clock);
            var calculations = new SensitivityCalculationService(constants);
            var warnings = new ErgonomicWarningService(new InjuryRiskFlagRepository(connectionFactory), calculations, constants, clock);
            return new ProfileSetupApplicationService(lifecycle, calculations, warnings,
                new ProfileDashboardService(new ProfileDashboardRepository(connectionFactory), warnings,
                    new ImmediateFeedbackService(new ImmediateFeedbackRepository(connectionFactory))),
                new CrossProfileComparisonService(new CrossProfileComparisonRepository(connectionFactory)));
        }
    }
}
