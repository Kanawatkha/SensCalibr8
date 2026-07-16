using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Services.Configuration;
using SensCalibr8.TestLogic;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P3R4TargetSequencingTests
    {
        private FrozenSequenceContract contract;
        private FrozenCalibrationConfiguration configuration;
        private DeterministicTargetSequencer sequencer;

        [SetUp]
        public void SetUp()
        {
            configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            contract = FrozenSequenceContract.From(configuration);
            sequencer = new DeterministicTargetSequencer(contract);
        }

        [Test]
        public void FrozenContractLoadsAcceptedSequenceAndSpawnSafetyValues()
        {
            Assert.That(contract.ModeVersion, Is.EqualTo("sc8-mode-contract-v1"));
            Assert.That(contract.Generator, Is.EqualTo("deterministic-versioned-seed"));
            Assert.That(contract.ShotTrials, Is.EqualTo(30));
            Assert.That(contract.TrackingBlocks, Is.EqualTo(2));
            Assert.That(contract.TrackingTrialsPerBlock, Is.EqualTo(9));
            Assert.That(contract.VerticalLimitDeg, Is.EqualTo(25d));
            Assert.That(contract.EdgeMarginPx, Is.EqualTo(32d));
            Assert.That(contract.HudReservePx, Is.EqualTo(64d));
        }

        [Test]
        public void SameSeedReproducesByteStableConditionSequence()
        {
            var context = Context(TestMode.FlickClose, 1);
            DeterministicTargetSequence first = sequencer.Create(context), second = sequencer.Create(context);
            Assert.That(first.Audit.SeedSha256, Is.EqualTo(second.Audit.SeedSha256));
            Assert.That(first.Audit.SeedMaterial, Is.EqualTo("sc8-mode-contract-v1|7|11|phase_one|flick_close|1"));
            Assert.That(Signature(first), Is.EqualTo(Signature(second)));
            Assert.That(first.Audit.SeedMaterial, Does.Not.Contain("sensitivity").And.Not.Contain("Candidate"));
        }

        [Test]
        public void FlickSequenceBalancesCrossProductRotatesExtrasAndStaysSpawnSafe()
        {
            DeterministicTargetSequence first = sequencer.Create(Context(TestMode.FlickFar, 1));
            DeterministicTargetSequence second = sequencer.Create(Context(TestMode.FlickFar, 2));
            Assert.That(first.Conditions.Count, Is.EqualTo(30));
            var counts = first.Conditions.GroupBy(value => value.TargetSize + ":" + value.CenterOffsetDeg).Select(group => group.Count()).ToArray();
            Assert.That(counts.All(count => count == 3 || count == 4), Is.True);
            Assert.That(counts.Count(count => count == 4), Is.EqualTo(3));
            Assert.That(first.Conditions.Max(value => Math.Abs(value.CenterElevationDeg.Value)), Is.LessThanOrEqualTo(25d));
            Assert.That(Signature(first), Is.Not.EqualTo(Signature(second)));
        }

        [Test]
        public void CloseForeperiodUsesOnlyFrozenInclusiveRange()
        {
            DeterministicTargetSequence sequence = sequencer.Create(Context(TestMode.FlickClose, 3));
            Assert.That(sequence.Conditions.All(value => value.ForeperiodMs >= 500d && value.ForeperiodMs <= 1000d), Is.True);
            Assert.That(sequence.Conditions.All(value => value.CenterXpx.HasValue && value.CenterYpx.HasValue), Is.True);
        }

        [Test]
        public void TrackingContainsOnePatternSizeCrossProductPerBlock()
        {
            DeterministicTargetSequence sequence = sequencer.Create(Context(TestMode.Tracking, 1));
            Assert.That(sequence.Conditions.Count, Is.EqualTo(18));
            foreach (IGrouping<int, TargetCondition> block in sequence.Conditions.GroupBy(value => value.BlockIndex))
            {
                Assert.That(block.Count(), Is.EqualTo(9));
                Assert.That(block.Select(value => value.Pattern + ":" + value.TargetSize).Distinct().Count(), Is.EqualTo(9));
            }
        }

        [Test]
        public void MicroCorrectionUsesSmallTargetsAndFrozenRadialPixelRange()
        {
            DeterministicTargetSequence sequence = sequencer.Create(Context(TestMode.MicroCorrection, 1));
            Assert.That(sequence.Conditions.Count, Is.EqualTo(30));
            foreach (TargetCondition condition in sequence.Conditions)
            {
                double dx = condition.CenterXpx.Value - contract.ViewportWidthPx / 2d;
                double dy = condition.CenterYpx.Value - contract.ViewportHeightPx / 2d;
                double radius = Math.Sqrt(dx * dx + dy * dy);
                Assert.That(condition.TargetSize, Is.EqualTo("small"));
                Assert.That(radius, Is.InRange(5d, 20d));
            }
        }

        [Test]
        public void CandidateLabelsHideSensitivityAndModeRotationsCounterbalanceFourRepetitions()
        {
            long[] candidates = { 10, 20, 30, 40, 50, 60, 70 };
            CounterbalancedOrder first = sequencer.CreateCounterbalancedOrder(7, 11, ProtocolPhase.PhaseOne, 1, candidates);
            Assert.That(first.Candidates.Select(value => value.CandidateId), Is.EquivalentTo(candidates));
            Assert.That(first.Candidates.Select(value => value.BlindLabel), Is.EqualTo(Enumerable.Range(1, 7).Select(value => "Candidate-" + value.ToString("D2"))));
            Assert.That(typeof(BlindCandidateAssignment).GetProperty("SensitivityValue"), Is.Null);
            var repetitions = Enumerable.Range(1, 4).Select(value => sequencer.CreateCounterbalancedOrder(7, 11, ProtocolPhase.PhaseOne, value, candidates).Modes).ToArray();
            for (int position = 0; position < 4; position++) Assert.That(repetitions.Select(order => order[position]).Distinct().Count(), Is.EqualTo(4));
        }

        [Test]
        public void ConfirmatoryOrderIsTheFrozenFiveFiveSequence()
        {
            ConfirmatoryOrderContract confirmatory = ConfirmatoryOrderContract.From(configuration);
            Assert.That(confirmatory.Version, Is.EqualTo("sc8-confirmatory-v1"));
            Assert.That(confirmatory.Order.Count(value => value == "A_then_B"), Is.EqualTo(5));
            Assert.That(confirmatory.Order.Count(value => value == "B_then_A"), Is.EqualTo(5));
            Assert.That(confirmatory.PairOrder(1), Is.EqualTo("A_then_B"));
            Assert.That(confirmatory.PairOrder(10), Is.EqualTo("B_then_A"));
            ConfirmatoryPairPlan forward = confirmatory.CreatePairPlan(7, 11, ProtocolPhase.PhaseOne, 280d, 294d, 1);
            ConfirmatoryPairPlan reversed = confirmatory.CreatePairPlan(7, 11, ProtocolPhase.PhaseOne, 294d, 280d, 1);
            Assert.That(forward.PairingSeed, Is.EqualTo(reversed.PairingSeed));
            Assert.That(forward.MatchedConditionKey, Is.EqualTo(reversed.MatchedConditionKey));
            Assert.That(forward.FirstCandidate, Is.EqualTo("A_then_B"));
            Assert.That(() => confirmatory.PairOrder(0), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void InvalidLineageInputsFailBeforeSequenceGeneration()
        {
            Assert.That(() => new SequenceSeedContext(0, 11, ProtocolPhase.PhaseOne, TestMode.FlickClose, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => sequencer.CreateCounterbalancedOrder(7, 11, ProtocolPhase.PhaseOne, 1, new long[] { 10, 10 }), Throws.TypeOf<ArgumentException>());
        }

        private static SequenceSeedContext Context(TestMode mode, int repetition) => new SequenceSeedContext(7, 11, ProtocolPhase.PhaseOne, mode, repetition);
        private static string Signature(DeterministicTargetSequence sequence) => string.Join(";", sequence.Conditions.Select(value => string.Join(":", value.TrialIndex, value.BlockIndex, value.TargetSize, value.Pattern, value.CenterOffsetDeg, value.CenterAzimuthDeg, value.CenterElevationDeg, value.CenterXpx, value.CenterYpx, value.ForeperiodMs)));
        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
