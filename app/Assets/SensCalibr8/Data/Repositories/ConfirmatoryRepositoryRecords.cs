using System;
using System.Collections.Generic;

namespace SensCalibr8.Data.Repositories
{
    public sealed class RankedProtocolCandidate
    {
        public RankedProtocolCandidate(ProtocolCandidateRecord candidate, double exploratoryMeanScore, long scoreCount)
        {
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            ExploratoryMeanScore = Finite(exploratoryMeanScore, nameof(exploratoryMeanScore));
            ScoreCount = scoreCount > 0 ? scoreCount : throw new ArgumentOutOfRangeException(nameof(scoreCount));
        }

        public ProtocolCandidateRecord Candidate { get; }
        public double ExploratoryMeanScore { get; }
        public long ScoreCount { get; }

        private static double Finite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(name);
    }

    public sealed class ConfirmatoryBatteryPairRecord
    {
        public ConfirmatoryBatteryPairRecord(ProtocolBatteryRecord candidateA, ProtocolBatteryRecord candidateB)
        {
            CandidateA = candidateA ?? throw new ArgumentNullException(nameof(candidateA));
            CandidateB = candidateB ?? throw new ArgumentNullException(nameof(candidateB));
        }

        public ProtocolBatteryRecord CandidateA { get; }
        public ProtocolBatteryRecord CandidateB { get; }
    }

    public sealed class SignificanceTestRecord
    {
        public SignificanceTestRecord(long? id, long profileId, long cycleId, long calibrationConfigId, long phase,
            double candidateAEdpi, double candidateBEdpi, string testMethod, string alternative, double alpha,
            double pValue, double effectEstimate, double confidenceLevel, double confidenceIntervalLower,
            double confidenceIntervalUpper, long pairedSampleSize, bool isSignificant, string formulaVersion, string result)
        {
            Id = id;
            ProfileId = Positive(profileId, nameof(profileId));
            CycleId = Positive(cycleId, nameof(cycleId));
            CalibrationConfigId = Positive(calibrationConfigId, nameof(calibrationConfigId));
            Phase = Positive(phase, nameof(phase));
            CandidateAEdpi = PositiveFinite(candidateAEdpi, nameof(candidateAEdpi));
            CandidateBEdpi = PositiveFinite(candidateBEdpi, nameof(candidateBEdpi));
            if (candidateAEdpi.Equals(candidateBEdpi)) throw new ArgumentException("Confirmatory candidates must be distinct.");
            TestMethod = Required(testMethod, nameof(testMethod));
            Alternative = Required(alternative, nameof(alternative));
            Alpha = Probability(alpha, nameof(alpha));
            PValue = InclusiveProbability(pValue, nameof(pValue));
            EffectEstimate = Finite(effectEstimate, nameof(effectEstimate));
            ConfidenceLevel = Probability(confidenceLevel, nameof(confidenceLevel));
            ConfidenceIntervalLower = Finite(confidenceIntervalLower, nameof(confidenceIntervalLower));
            ConfidenceIntervalUpper = Finite(confidenceIntervalUpper, nameof(confidenceIntervalUpper));
            if (confidenceIntervalUpper < confidenceIntervalLower) throw new ArgumentException("Confidence interval is reversed.");
            PairedSampleSize = Positive(pairedSampleSize, nameof(pairedSampleSize));
            IsSignificant = isSignificant;
            FormulaVersion = Required(formulaVersion, nameof(formulaVersion));
            Result = Required(result, nameof(result));
            if (isSignificant != (pValue < alpha)) throw new ArgumentException("Significance flag must match strict p < alpha.");
            if (result == "statistical_tie")
            {
                if (isSignificant) throw new ArgumentException("A statistical tie cannot be significant.");
            }
            else if (result == "candidate_a")
            {
                if (!isSignificant || effectEstimate <= 0d) throw new ArgumentException("Candidate A requires a significant positive effect.");
            }
            else if (result == "candidate_b")
            {
                if (!isSignificant || effectEstimate >= 0d) throw new ArgumentException("Candidate B requires a significant negative effect.");
            }
            else throw new ArgumentException("Unsupported significance result.", nameof(result));
        }

        public long? Id { get; }
        public long ProfileId { get; }
        public long CycleId { get; }
        public long CalibrationConfigId { get; }
        public long Phase { get; }
        public double CandidateAEdpi { get; }
        public double CandidateBEdpi { get; }
        public string TestMethod { get; }
        public string Alternative { get; }
        public double Alpha { get; }
        public double PValue { get; }
        public double EffectEstimate { get; }
        public double ConfidenceLevel { get; }
        public double ConfidenceIntervalLower { get; }
        public double ConfidenceIntervalUpper { get; }
        public long PairedSampleSize { get; }
        public bool IsSignificant { get; }
        public string FormulaVersion { get; }
        public string Result { get; }

        private static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
        private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(name + " is required.", name);
        private static double Probability(double value, string name) => PositiveFinite(value, name) < 1d ? value : throw new ArgumentOutOfRangeException(name);
        private static double InclusiveProbability(double value, string name) => Finite(value, name) >= 0d && value <= 1d ? value : throw new ArgumentOutOfRangeException(name);
        private static double PositiveFinite(double value, string name) => Finite(value, name) > 0d ? value : throw new ArgumentOutOfRangeException(name);
        private static double Finite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(name);
    }

    public sealed class SignificanceTestPairRecord
    {
        public SignificanceTestPairRecord(int pairIndex, string firstCandidate, string pairingSeed,
            string matchedConditionKey, long candidateABatteryId, long candidateBBatteryId,
            double candidateAScore, double candidateBScore)
        {
            PairIndex = pairIndex > 0 ? pairIndex : throw new ArgumentOutOfRangeException(nameof(pairIndex));
            FirstCandidate = Required(firstCandidate, nameof(firstCandidate));
            PairingSeed = Required(pairingSeed, nameof(pairingSeed));
            MatchedConditionKey = Required(matchedConditionKey, nameof(matchedConditionKey));
            CandidateABatteryId = Positive(candidateABatteryId, nameof(candidateABatteryId));
            CandidateBBatteryId = Positive(candidateBBatteryId, nameof(candidateBBatteryId));
            if (candidateABatteryId == candidateBBatteryId) throw new ArgumentException("Paired batteries must be distinct.");
            CandidateAScore = Finite(candidateAScore, nameof(candidateAScore));
            CandidateBScore = Finite(candidateBScore, nameof(candidateBScore));
        }

        public int PairIndex { get; }
        public string FirstCandidate { get; }
        public string PairingSeed { get; }
        public string MatchedConditionKey { get; }
        public long CandidateABatteryId { get; }
        public long CandidateBBatteryId { get; }
        public double CandidateAScore { get; }
        public double CandidateBScore { get; }

        private static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
        private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(name + " is required.", name);
        private static double Finite(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(name);
    }

    public sealed class PersistedSignificanceTest
    {
        public PersistedSignificanceTest(SignificanceTestRecord test, IReadOnlyList<SignificanceTestPairRecord> pairs)
        {
            Test = test ?? throw new ArgumentNullException(nameof(test));
            Pairs = pairs ?? throw new ArgumentNullException(nameof(pairs));
        }

        public SignificanceTestRecord Test { get; }
        public IReadOnlyList<SignificanceTestPairRecord> Pairs { get; }
    }
}
