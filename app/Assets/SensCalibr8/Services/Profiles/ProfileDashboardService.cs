using System;
using System.Collections.Generic;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Analysis;

namespace SensCalibr8.Services.Profiles
{
    public sealed class ProfileDashboardPresentation
    {
        public ProfileDashboardPresentation(ProfileSetupPresentation profile, long completedSessionCount, string latestSessionDate,
            string latestGrade, IReadOnlyList<ErgonomicWarningPresentation> warnings, ImmediateFeedbackPresentation immediateFeedback = null)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile)); CompletedSessionCount = completedSessionCount;
            LatestSessionDate = latestSessionDate; LatestGrade = latestGrade; Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));
            ImmediateFeedback = immediateFeedback ?? ImmediateFeedbackPresentation.Empty;
        }
        public ProfileSetupPresentation Profile { get; } public long CompletedSessionCount { get; } public string LatestSessionDate { get; }
        public string LatestGrade { get; } public IReadOnlyList<ErgonomicWarningPresentation> Warnings { get; }
        public ImmediateFeedbackPresentation ImmediateFeedback { get; }
    }

    public sealed class ProfileDashboardService
    {
        private readonly ProfileDashboardRepository summaries;
        private readonly ErgonomicWarningService warnings;
        private readonly ImmediateFeedbackService feedback;

        public ProfileDashboardService(ProfileDashboardRepository summaries, ErgonomicWarningService warnings)
            : this(summaries, warnings, null) { }

        public ProfileDashboardService(ProfileDashboardRepository summaries, ErgonomicWarningService warnings, ImmediateFeedbackService feedback)
        {
            this.summaries = summaries ?? throw new ArgumentNullException(nameof(summaries));
            this.warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));
            this.feedback = feedback;
        }

        public ProfileDashboardPresentation Load(ProfileSetupPresentation profile)
        {
            ProfileDashboardRecord summary = summaries.Get(profile.Id);
            ImmediateFeedbackPresentation current = feedback == null ? ImmediateFeedbackPresentation.Empty : feedback.Load(profile.Id);
            return new ProfileDashboardPresentation(profile, summary.CompletedSessionCount, summary.LatestSessionDate, summary.LatestGrade,
                warnings.EvaluateAndList(profile), current);
        }
    }
}
