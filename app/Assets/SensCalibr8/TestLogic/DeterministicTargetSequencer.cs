using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;

namespace SensCalibr8.TestLogic
{
    public sealed class FrozenSequenceContract
    {
        private FrozenSequenceContract(string modeVersion, string generator, int shotTrials, int completeShotBlocks,
            int trackingBlocks, int trackingTrialsPerBlock, double verticalLimitDeg, double focalLengthPx,
            double viewportWidthPx, double viewportHeightPx, double edgeMarginPx, double hudReservePx,
            double microMinPx, double microMaxPx, double closeForeperiodMinMs, double closeForeperiodMaxMs,
            IReadOnlyDictionary<TestMode, IReadOnlyList<double>> offsetsByMode,
            IReadOnlyList<string> targetSizes, IReadOnlyList<string> trackingPatterns,
            IReadOnlyDictionary<string, double> targetPixelDiameters)
        {
            ModeVersion = modeVersion; Generator = generator; ShotTrials = shotTrials; CompleteShotBlocks = completeShotBlocks;
            TrackingBlocks = trackingBlocks; TrackingTrialsPerBlock = trackingTrialsPerBlock; VerticalLimitDeg = verticalLimitDeg;
            FocalLengthPx = focalLengthPx; ViewportWidthPx = viewportWidthPx; ViewportHeightPx = viewportHeightPx;
            EdgeMarginPx = edgeMarginPx; HudReservePx = hudReservePx; MicroMinPx = microMinPx; MicroMaxPx = microMaxPx;
            CloseForeperiodMinMs = closeForeperiodMinMs; CloseForeperiodMaxMs = closeForeperiodMaxMs;
            OffsetsByMode = offsetsByMode; TargetSizes = targetSizes; TrackingPatterns = trackingPatterns;
            TargetPixelDiameters = targetPixelDiameters;
        }

        public string ModeVersion { get; } public string Generator { get; } public int ShotTrials { get; }
        public int CompleteShotBlocks { get; } public int TrackingBlocks { get; } public int TrackingTrialsPerBlock { get; }
        public double VerticalLimitDeg { get; } public double FocalLengthPx { get; } public double ViewportWidthPx { get; }
        public double ViewportHeightPx { get; } public double EdgeMarginPx { get; } public double HudReservePx { get; }
        public double MicroMinPx { get; } public double MicroMaxPx { get; } public double CloseForeperiodMinMs { get; }
        public double CloseForeperiodMaxMs { get; }
        public IReadOnlyDictionary<TestMode, IReadOnlyList<double>> OffsetsByMode { get; }
        public IReadOnlyList<string> TargetSizes { get; } public IReadOnlyList<string> TrackingPatterns { get; }
        public IReadOnlyDictionary<string, double> TargetPixelDiameters { get; }

        public static FrozenSequenceContract From(FrozenCalibrationConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            try
            {
                using JsonDocument modesDoc = JsonDocument.Parse(configuration.Record.TrackingContractJson);
                using JsonDocument geometryDoc = JsonDocument.Parse(configuration.Record.TargetGeometryJson);
                JsonElement root = modesDoc.RootElement, geometry = geometryDoc.RootElement;
                JsonElement policy = root.GetProperty("sequence_policy"), modes = root.GetProperty("modes");
                string version = root.GetProperty("mode_contract_version").GetString();
                string generator = policy.GetProperty("generator").GetString();
                RequireSequencePolicy(policy, version, generator, configuration.Record.TestGeometryVersion, root.GetProperty("dependencies"));

                JsonElement shared = root.GetProperty("shared_shot_contract");
                int shotTrials = shared.GetProperty("minimum_resolved_trials").GetInt32();
                IReadOnlyList<string> sizes = Strings(modes.GetProperty("flick_close").GetProperty("target_sizes"));
                int conditionCount = sizes.Count * modes.GetProperty("flick_close").GetProperty("center_offsets_deg").GetArrayLength();
                int completeBlocks = shotTrials / conditionCount;
                JsonElement tracking = modes.GetProperty("tracking"), camera = geometry.GetProperty("camera"), viewport = geometry.GetProperty("reference_viewport"), safety = geometry.GetProperty("spawn_safety"), target = geometry.GetProperty("target");
                var diameters = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (JsonProperty size in target.GetProperty("sizes").EnumerateObject()) diameters.Add(size.Name, size.Value.GetProperty("reference_pixel_diameter").GetDouble());
                var offsets = new Dictionary<TestMode, IReadOnlyList<double>>
                {
                    [TestMode.FlickClose] = Numbers(modes.GetProperty("flick_close").GetProperty("center_offsets_deg")),
                    [TestMode.FlickFar] = Numbers(modes.GetProperty("flick_far").GetProperty("center_offsets_deg"))
                };
                JsonElement foreperiod = modes.GetProperty("flick_close").GetProperty("foreperiod_ms"), micro = modes.GetProperty("micro_correction").GetProperty("offset_px");
                if (shotTrials <= 0 || conditionCount <= 0 || completeBlocks <= 0 || shotTrials < completeBlocks * conditionCount)
                    throw new InvalidDataException("Frozen shot sequence balance is invalid.");
                return new FrozenSequenceContract(version, generator, shotTrials, completeBlocks,
                    tracking.GetProperty("blocks").GetInt32(), tracking.GetProperty("trials_per_block").GetInt32(),
                    geometry.GetProperty("flick_conditions").GetProperty("vertical_center_limit_deg").GetDouble(),
                    camera.GetProperty("reference_focal_length_px").GetDouble(), viewport.GetProperty("width_px").GetDouble(), viewport.GetProperty("height_px").GetDouble(),
                    safety.GetProperty("edge_margin_px").GetDouble(), safety.GetProperty("hud_reserved_top_px").GetDouble(),
                    micro.GetProperty("minimum").GetDouble(), micro.GetProperty("maximum").GetDouble(), foreperiod.GetProperty("minimum").GetDouble(), foreperiod.GetProperty("maximum").GetDouble(),
                    new ReadOnlyDictionary<TestMode, IReadOnlyList<double>>(offsets), sizes, Strings(tracking.GetProperty("patterns")), new ReadOnlyDictionary<string, double>(diameters));
            }
            catch (JsonException exception) { throw new InvalidDataException("Frozen sequence contracts are invalid.", exception); }
        }

        private static void RequireSequencePolicy(JsonElement policy, string version, string generator, string geometryVersion, JsonElement dependencies)
        {
            if (string.IsNullOrWhiteSpace(version) || !string.Equals(generator, "deterministic-versioned-seed", StringComparison.Ordinal) ||
                !policy.GetProperty("same_condition_sequence_across_sensitivity_candidates").GetBoolean() ||
                !ContainsExactly(policy.GetProperty("seed_includes"), "mode_contract_version", "profile_id", "cycle_id", "phase", "mode", "battery_repetition_ordinal") ||
                !ContainsExactly(policy.GetProperty("seed_excludes"), "sensitivity_value", "blind_candidate_label") ||
                !string.Equals(dependencies.GetProperty("test_geometry_version").GetString(), geometryVersion, StringComparison.Ordinal))
                throw new InvalidDataException("Frozen deterministic sequence policy is incomplete or unsupported.");
        }

        private static bool ContainsExactly(JsonElement values, params string[] expected)
        {
            var actual = new HashSet<string>(Strings(values), StringComparer.Ordinal);
            return actual.SetEquals(expected);
        }
        private static IReadOnlyList<string> Strings(JsonElement values) => values.EnumerateArray().Select(value => value.GetString()).ToArray();
        private static IReadOnlyList<double> Numbers(JsonElement values) => values.EnumerateArray().Select(value => value.GetDouble()).ToArray();
    }

    public sealed class SequenceSeedContext
    {
        public SequenceSeedContext(long profileId, long cycleId, ProtocolPhase phase, TestMode mode, int batteryRepetitionOrdinal)
        {
            ProfileId = profileId > 0 ? profileId : throw new ArgumentOutOfRangeException(nameof(profileId));
            CycleId = cycleId > 0 ? cycleId : throw new ArgumentOutOfRangeException(nameof(cycleId));
            Phase = Enum.IsDefined(typeof(ProtocolPhase), phase) ? phase : throw new ArgumentOutOfRangeException(nameof(phase));
            Mode = Enum.IsDefined(typeof(TestMode), mode) ? mode : throw new ArgumentOutOfRangeException(nameof(mode));
            BatteryRepetitionOrdinal = batteryRepetitionOrdinal > 0 ? batteryRepetitionOrdinal : throw new ArgumentOutOfRangeException(nameof(batteryRepetitionOrdinal));
        }
        public long ProfileId { get; } public long CycleId { get; } public ProtocolPhase Phase { get; }
        public TestMode Mode { get; } public int BatteryRepetitionOrdinal { get; }
    }

    public sealed class SequenceAuditMetadata
    {
        public SequenceAuditMetadata(string generator, string contractVersion, string seedSha256, string seedMaterial)
        { Generator = Required(generator); ContractVersion = Required(contractVersion); SeedSha256 = Required(seedSha256); SeedMaterial = Required(seedMaterial); }
        public string Generator { get; } public string ContractVersion { get; } public string SeedSha256 { get; } public string SeedMaterial { get; }
        private static string Required(string value) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Sequence audit value is required.");
    }

    public sealed class TargetCondition
    {
        public TargetCondition(int trialIndex, int blockIndex, TestMode mode, string targetSize, string pattern,
            double? centerOffsetDeg, double? centerAzimuthDeg, double? centerElevationDeg, double? centerXpx,
            double? centerYpx, double? foreperiodMs)
        { TrialIndex=trialIndex; BlockIndex=blockIndex; Mode=mode; TargetSize=targetSize; Pattern=pattern; CenterOffsetDeg=centerOffsetDeg;
          CenterAzimuthDeg=centerAzimuthDeg; CenterElevationDeg=centerElevationDeg; CenterXpx=centerXpx; CenterYpx=centerYpx; ForeperiodMs=foreperiodMs; }
        public int TrialIndex { get; } public int BlockIndex { get; } public TestMode Mode { get; } public string TargetSize { get; }
        public string Pattern { get; } public double? CenterOffsetDeg { get; } public double? CenterAzimuthDeg { get; }
        public double? CenterElevationDeg { get; } public double? CenterXpx { get; } public double? CenterYpx { get; }
        public double? ForeperiodMs { get; }
    }

    public sealed class DeterministicTargetSequence
    {
        public DeterministicTargetSequence(SequenceAuditMetadata audit, IReadOnlyList<TargetCondition> conditions)
        { Audit=audit??throw new ArgumentNullException(nameof(audit)); Conditions=conditions??throw new ArgumentNullException(nameof(conditions)); }
        public SequenceAuditMetadata Audit { get; } public IReadOnlyList<TargetCondition> Conditions { get; }
    }

    public sealed class BlindCandidateAssignment
    {
        public BlindCandidateAssignment(long candidateId, string blindLabel, int orderIndex)
        { CandidateId=candidateId>0?candidateId:throw new ArgumentOutOfRangeException(nameof(candidateId)); BlindLabel=!string.IsNullOrWhiteSpace(blindLabel)?blindLabel:throw new ArgumentException(nameof(blindLabel)); OrderIndex=orderIndex; }
        public long CandidateId { get; } public string BlindLabel { get; } public int OrderIndex { get; }
    }

    public sealed class CounterbalancedOrder
    {
        public CounterbalancedOrder(SequenceAuditMetadata audit, IReadOnlyList<BlindCandidateAssignment> candidates, IReadOnlyList<TestMode> modes)
        { Audit=audit; Candidates=candidates; Modes=modes; }
        public SequenceAuditMetadata Audit { get; } public IReadOnlyList<BlindCandidateAssignment> Candidates { get; } public IReadOnlyList<TestMode> Modes { get; }
    }

    public sealed class DeterministicTargetSequencer
    {
        private readonly FrozenSequenceContract contract;
        public DeterministicTargetSequencer(FrozenSequenceContract contract) { this.contract=contract??throw new ArgumentNullException(nameof(contract)); }

        public DeterministicTargetSequence Create(SequenceSeedContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            SequenceAuditMetadata audit = Audit(context);
            IReadOnlyList<TargetCondition> conditions = context.Mode == TestMode.Tracking ? Tracking(context, audit.SeedSha256) : context.Mode == TestMode.MicroCorrection ? Micro(context, audit.SeedSha256) : Flick(context, audit.SeedSha256);
            return new DeterministicTargetSequence(audit, conditions);
        }

        public CounterbalancedOrder CreateCounterbalancedOrder(long profileId, long cycleId, ProtocolPhase phase,
            int batteryRepetitionOrdinal, IReadOnlyList<long> candidateIds)
        {
            if (profileId <= 0) throw new ArgumentOutOfRangeException(nameof(profileId));
            if (cycleId <= 0) throw new ArgumentOutOfRangeException(nameof(cycleId));
            if (!Enum.IsDefined(typeof(ProtocolPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            if (batteryRepetitionOrdinal <= 0) throw new ArgumentOutOfRangeException(nameof(batteryRepetitionOrdinal));
            if (candidateIds == null || candidateIds.Count == 0 || candidateIds.Any(id => id <= 0) || candidateIds.Distinct().Count() != candidateIds.Count)
                throw new ArgumentException("Unique positive candidate identifiers are required.", nameof(candidateIds));
            string material = string.Join("|", contract.ModeVersion, profileId.ToString(CultureInfo.InvariantCulture), cycleId.ToString(CultureInfo.InvariantCulture), PhaseName(phase), "counterbalanced-order");
            string seed = Hash(material); var ordered = candidateIds.OrderBy(id => Hash(seed + "|candidate|" + id.ToString(CultureInfo.InvariantCulture)), StringComparer.Ordinal).ToArray();
            var assignments = ordered.Select((id,index) => new BlindCandidateAssignment(id, "Candidate-" + (index+1).ToString("D2",CultureInfo.InvariantCulture), index)).ToArray();
            TestMode[] baseModes = Enum.GetValues(typeof(TestMode)).Cast<TestMode>().OrderBy(mode => Hash(seed + "|mode|" + ModeName(mode)),StringComparer.Ordinal).ToArray();
            int rotation = (batteryRepetitionOrdinal - 1) % baseModes.Length; TestMode[] modes = baseModes.Skip(rotation).Concat(baseModes.Take(rotation)).ToArray();
            return new CounterbalancedOrder(new SequenceAuditMetadata(contract.Generator,contract.ModeVersion,seed,material),assignments,modes);
        }

        private IReadOnlyList<TargetCondition> Flick(SequenceSeedContext context, string seed)
        {
            IReadOnlyList<double> offsets = contract.OffsetsByMode[context.Mode]; var baseConditions = new List<(double offset,string size)>();
            foreach(double offset in offsets) foreach(string size in contract.TargetSizes) baseConditions.Add((offset,size));
            var selected = new List<(double offset,string size,int block)>();
            for(int block=0;block<contract.CompleteShotBlocks;block++) selected.AddRange(Shuffle(baseConditions,seed+"|block|"+block).Select(value=>(value.offset,value.size,block)));
            int extras = contract.ShotTrials-selected.Count, start=((context.BatteryRepetitionOrdinal-1)*extras)%baseConditions.Count;
            for(int i=0;i<extras;i++){var value=baseConditions[(start+i)%baseConditions.Count];selected.Add((value.offset,value.size,contract.CompleteShotBlocks));}
            return selected.Select((value,index)=>FlickCondition(context.Mode,index,value.block,value.offset,value.size,seed)).ToArray();
        }

        private TargetCondition FlickCondition(TestMode mode,int index,int block,double offset,string size,string seed)
        {
            double angle=Unit(seed+"|direction|"+index)*Math.PI*2d, verticalAmplitude=Math.Min(offset,contract.VerticalLimitDeg);
            double elevation=Math.Sin(angle)*verticalAmplitude, azimuth=Math.Sign(Math.Cos(angle))*Math.Sqrt(Math.Max(0d,offset*offset-elevation*elevation));
            (double x,double y)=Project(azimuth,elevation); EnsureSafe(size,x,y);
            double? foreperiod=mode==TestMode.FlickClose?contract.CloseForeperiodMinMs+Unit(seed+"|foreperiod|"+index)*(contract.CloseForeperiodMaxMs-contract.CloseForeperiodMinMs):(double?)null;
            return new TargetCondition(index,block,mode,size,null,offset,azimuth,elevation,x,y,foreperiod);
        }

        private IReadOnlyList<TargetCondition> Tracking(SequenceSeedContext context,string seed)
        {
            var values=new List<(string pattern,string size)>(); foreach(string pattern in contract.TrackingPatterns)foreach(string size in contract.TargetSizes)values.Add((pattern,size));
            var result=new List<TargetCondition>();
            for(int block=0;block<contract.TrackingBlocks;block++) foreach(var value in Shuffle(values,seed+"|block|"+block)) result.Add(new TargetCondition(result.Count,block,TestMode.Tracking,value.size,value.pattern,null,null,null,null,null,null));
            if(result.Count!=contract.TrackingBlocks*contract.TrackingTrialsPerBlock)throw new InvalidDataException("Tracking sequence does not match the frozen block contract.");
            return result;
        }

        private IReadOnlyList<TargetCondition> Micro(SequenceSeedContext context,string seed)
        {
            var result=new List<TargetCondition>(); string size=contract.TargetSizes[0];
            for(int index=0;index<contract.ShotTrials;index++) { double radius=contract.MicroMinPx+Unit(seed+"|radius|"+index)*(contract.MicroMaxPx-contract.MicroMinPx), angle=Unit(seed+"|direction|"+index)*Math.PI*2d;
                double x=contract.ViewportWidthPx/2d+Math.Cos(angle)*radius,y=contract.ViewportHeightPx/2d-Math.Sin(angle)*radius;EnsureSafe(size,x,y);result.Add(new TargetCondition(index,index/contract.ShotTrials,TestMode.MicroCorrection,size,null,null,null,null,x,y,null)); }
            return result;
        }

        private SequenceAuditMetadata Audit(SequenceSeedContext context)
        {
            string material=string.Join("|",contract.ModeVersion,context.ProfileId.ToString(CultureInfo.InvariantCulture),context.CycleId.ToString(CultureInfo.InvariantCulture),PhaseName(context.Phase),ModeName(context.Mode),context.BatteryRepetitionOrdinal.ToString(CultureInfo.InvariantCulture));
            return new SequenceAuditMetadata(contract.Generator,contract.ModeVersion,Hash(material),material);
        }
        private (double x,double y) Project(double azimuth,double elevation) => (contract.ViewportWidthPx/2d+contract.FocalLengthPx*Math.Tan(azimuth*Math.PI/180d),contract.ViewportHeightPx/2d-contract.FocalLengthPx*Math.Tan(elevation*Math.PI/180d));
        private void EnsureSafe(string size,double x,double y) { double radius=contract.TargetPixelDiameters[size]/2d; if(x-radius<contract.EdgeMarginPx||x+radius>contract.ViewportWidthPx-contract.EdgeMarginPx||y-radius<contract.HudReservePx||y+radius>contract.ViewportHeightPx-contract.EdgeMarginPx)throw new InvalidDataException("Deterministic target violates the frozen spawn-safe viewport."); }
        private static IReadOnlyList<T> Shuffle<T>(IEnumerable<T> values,string seed) => values.Select((value,index)=>(value,key:Hash(seed+"|"+index))).OrderBy(item=>item.key,StringComparer.Ordinal).Select(item=>item.value).ToArray();
        private static double Unit(string value) { byte[] hash; using(SHA256 algorithm=SHA256.Create()) hash=algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)); ulong raw=0; for(int index=0;index<8;index++) raw=(raw<<8)|hash[index]; raw>>=11; return raw/(double)(1UL<<53); }
        private static string Hash(string value) { using SHA256 algorithm=SHA256.Create(); return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-",string.Empty).ToLowerInvariant(); }
        private static string PhaseName(ProtocolPhase phase) { switch(phase){case ProtocolPhase.PhaseOne:return "phase_one";case ProtocolPhase.PhaseTwo:return "phase_two";case ProtocolPhase.PhaseThree:return "phase_three";default:throw new ArgumentOutOfRangeException(nameof(phase));} }
        private static string ModeName(TestMode mode) { switch(mode){case TestMode.FlickClose:return "flick_close";case TestMode.FlickFar:return "flick_far";case TestMode.Tracking:return "tracking";case TestMode.MicroCorrection:return "micro_correction";default:throw new ArgumentOutOfRangeException(nameof(mode));} }
    }

    public sealed class ConfirmatoryOrderContract
    {
        private ConfirmatoryOrderContract(string version,IReadOnlyList<string> order){Version=version;Order=order;}
        public string Version{get;} public IReadOnlyList<string> Order{get;}
        public static ConfirmatoryOrderContract From(FrozenCalibrationConfiguration configuration)
        { if(configuration==null)throw new ArgumentNullException(nameof(configuration));using JsonDocument document=JsonDocument.Parse(configuration.Record.ConfirmatoryContractJson);JsonElement root=document.RootElement;string version=root.GetProperty("confirmatory_contract_version").GetString();IReadOnlyList<string> order=root.GetProperty("order_sequence").EnumerateArray().Select(value=>value.GetString()).ToArray();int required=root.GetProperty("fresh_pairs_required").GetInt32();string[] expectedInputs={"confirmatory_contract_version","profile_id","cycle_id","phase","sorted_candidate_edpi_pair"};var inputs=new HashSet<string>(root.GetProperty("order_seed_inputs").EnumerateArray().Select(value=>value.GetString()),StringComparer.Ordinal);if(order.Count!=required||order.Count(value=>value=="A_then_B")!=root.GetProperty("candidate_order_balance").GetProperty("A_then_B").GetInt32()||order.Count(value=>value=="B_then_A")!=root.GetProperty("candidate_order_balance").GetProperty("B_then_A").GetInt32()||!inputs.SetEquals(expectedInputs)||!string.Equals(root.GetProperty("pairing").GetString(),"same-four-mode-order-and-condition-seeds-within-pair",StringComparison.Ordinal))throw new InvalidDataException("Confirmatory order is not balanced.");return new ConfirmatoryOrderContract(version,order); }
        public string PairOrder(int oneBasedPairIndex){if(oneBasedPairIndex<1||oneBasedPairIndex>Order.Count)throw new ArgumentOutOfRangeException(nameof(oneBasedPairIndex));return Order[oneBasedPairIndex-1];}
        public ConfirmatoryPairPlan CreatePairPlan(long profileId,long cycleId,ProtocolPhase phase,double candidateAEdpi,double candidateBEdpi,int oneBasedPairIndex)
        { if(profileId<=0)throw new ArgumentOutOfRangeException(nameof(profileId));if(cycleId<=0)throw new ArgumentOutOfRangeException(nameof(cycleId));if(!Enum.IsDefined(typeof(ProtocolPhase),phase))throw new ArgumentOutOfRangeException(nameof(phase));if(!PositiveFinite(candidateAEdpi)||!PositiveFinite(candidateBEdpi)||candidateAEdpi.Equals(candidateBEdpi))throw new ArgumentException("Two distinct positive finite candidate eDPI values are required.");string first=PairOrder(oneBasedPairIndex);double low=Math.Min(candidateAEdpi,candidateBEdpi),high=Math.Max(candidateAEdpi,candidateBEdpi);string phaseName=phase==ProtocolPhase.PhaseOne?"phase_one":phase==ProtocolPhase.PhaseTwo?"phase_two":"phase_three";string material=string.Join("|",Version,profileId.ToString(CultureInfo.InvariantCulture),cycleId.ToString(CultureInfo.InvariantCulture),phaseName,low.ToString("G17",CultureInfo.InvariantCulture),high.ToString("G17",CultureInfo.InvariantCulture));string seed=HashValue(material);return new ConfirmatoryPairPlan(oneBasedPairIndex,first,seed,HashValue(seed+"|pair|"+oneBasedPairIndex.ToString(CultureInfo.InvariantCulture))); }
        private static bool PositiveFinite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)&&value>0d;
        private static string HashValue(string value){using SHA256 algorithm=SHA256.Create();return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-",string.Empty).ToLowerInvariant();}
    }

    public sealed class ConfirmatoryPairPlan
    { public ConfirmatoryPairPlan(int pairIndex,string firstCandidate,string pairingSeed,string matchedConditionKey){PairIndex=pairIndex;FirstCandidate=firstCandidate;PairingSeed=pairingSeed;MatchedConditionKey=matchedConditionKey;}public int PairIndex{get;}public string FirstCandidate{get;}public string PairingSeed{get;}public string MatchedConditionKey{get;} }
}
