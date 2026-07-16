using System;
using System.Collections.Generic;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Profiles
{
    public sealed class ProfileDashboardPresentation
    {
        public ProfileDashboardPresentation(ProfileSetupPresentation profile, long completedSessionCount, string latestSessionDate,
            string latestGrade, IReadOnlyList<ErgonomicWarningPresentation> warnings)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile)); CompletedSessionCount = completedSessionCount;
            LatestSessionDate = latestSessionDate; LatestGrade = latestGrade; Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));
        }
        public ProfileSetupPresentation Profile { get; } public long CompletedSessionCount { get; } public string LatestSessionDate { get; }
        public string LatestGrade { get; } public IReadOnlyList<ErgonomicWarningPresentation> Warnings { get; }
    }

    public sealed class ProfileDashboardService
    {
        private readonly ProfileDashboardRepository summaries;
        private readonly ErgonomicWarningService warnings;

        public ProfileDashboardService(ProfileDashboardRepository summaries, ErgonomicWarningService warnings)
        { this.summaries = summaries ?? throw new ArgumentNullException(nameof(summaries)); this.warnings = warnings ?? throw new ArgumentNullException(nameof(warnings)); }

        public ProfileDashboardPresentation Load(ProfileSetupPresentation profile)
        {
            ProfileDashboardRecord summary = summaries.Get(profile.Id);
            return new ProfileDashboardPresentation(profile, summary.CompletedSessionCount, summary.LatestSessionDate, summary.LatestGrade, warnings.EvaluateAndList(profile));
        }
    }
}
