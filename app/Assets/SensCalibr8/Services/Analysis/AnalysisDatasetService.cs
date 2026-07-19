using System;
using System.Text.Json;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Analysis
{
    public sealed class AnalysisDatasetService
    {
        private readonly AnalysisReadRepository repository;

        public AnalysisDatasetService(AnalysisReadRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public AnalysisProfileDataset ReadProfileDataset(long profileId) => repository.LoadProfileDataset(profileId);

        public string SerializeProfileDataset(long profileId)
        {
            AnalysisProfileDataset dataset = ReadProfileDataset(profileId);
            return JsonSerializer.Serialize(dataset, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
        }
    }
}
