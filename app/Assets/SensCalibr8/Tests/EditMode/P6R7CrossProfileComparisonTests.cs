using System;
using System.Collections.Generic;
using NUnit.Framework;
using SensCalibr8.Data.Repositories;
using SensCalibr8.Services.Analysis;

namespace SensCalibr8.Tests.EditMode
{
    public sealed class P6R7CrossProfileComparisonTests
    {
        [Test] public void ComparisonReadsOnlyExplicitProfilesAndUsesPersistedNormalizedMetrics()
        {
            var reader=new FakeReader();
            var service=new CrossProfileComparisonService(reader);
            IReadOnlyList<ProfileComparisonPresentation> rows=service.LoadExplicit(new long[]{4,9});
            Assert.That(reader.Requested.Count,Is.EqualTo(2));
            Assert.That(reader.Requested[0],Is.EqualTo(4));
            Assert.That(reader.Requested[1],Is.EqualTo(9));
            Assert.That(rows.Count,Is.EqualTo(2));
            Assert.That(rows[0].Edpi,Is.EqualTo(280d));
            Assert.That(rows[0].ConsistencyUtility,Is.EqualTo(0.8d));
            Assert.That(rows[0].ReactionTier,Is.EqualTo("A"));
            Assert.That(rows[0].PerformanceScore,Is.EqualTo(77d));
            Assert.That(rows[0].HasComparableResult,Is.True);
            Assert.That(rows[1].HasComparableResult,Is.False);
        }
        [Test] public void ComparisonRejectsFewerThanTwoDuplicateOrInvalidSelections()
        {
            var service=new CrossProfileComparisonService(new FakeReader());
            Assert.That(()=>service.LoadExplicit(new long[]{4}),Throws.TypeOf<ArgumentException>());
            Assert.That(()=>service.LoadExplicit(new long[]{4,4}),Throws.TypeOf<ArgumentException>());
            Assert.That(()=>service.LoadExplicit(new long[]{0,4}),Throws.TypeOf<ArgumentException>());
        }
        private sealed class FakeReader : ICrossProfileComparisonReader
        {
            public IReadOnlyList<long> Requested{get;private set;}
            public IReadOnlyList<CrossProfileComparisonData> ReadExplicit(IReadOnlyList<long> ids)
            {
                Requested=new List<long>(ids).AsReadOnly();
                return new[]{new CrossProfileComparisonData(4,"Pilot",280d,0.8d,"A",77d,"score-v1","config-v1","2026-07-19"),new CrossProfileComparisonData(9,"Peer",null,null,null,null,null,null,null)};
            }
        }
    }
}
