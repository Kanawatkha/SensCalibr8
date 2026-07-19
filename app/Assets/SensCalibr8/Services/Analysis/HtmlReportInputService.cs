using System;
using System.Text.Json;
using SensCalibr8.Data.Repositories;

namespace SensCalibr8.Services.Analysis
{
    public sealed class HtmlReportInputService
    {
        private readonly HtmlReportInputRepository repository;
        public HtmlReportInputService(HtmlReportInputRepository repository){this.repository=repository??throw new ArgumentNullException(nameof(repository));}
        public HtmlReportInput Read(long profileId)=>repository.Read(profileId);
        public string Serialize(long profileId)=>JsonSerializer.Serialize(Read(profileId),new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true});
    }
}
