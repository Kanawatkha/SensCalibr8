using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;

namespace SensCalibr8.TestLogic
{
    public sealed class FrozenSubmovementContract
    {
        private FrozenSubmovementContract(string version,double sampling,double start,double end,double refractory,int pad,int minimum,IReadOnlyList<double[]> sos)
        { SignalPipelineVersion=version;SamplingRateHz=sampling;StartThresholdDegPerSec=start;EndThresholdDegPerSec=end;RefractoryPeriodMs=refractory;PadLengthSamples=pad;MinimumFilterableSegmentSamples=minimum;Sos=sos; }
        public string SignalPipelineVersion{get;} public double SamplingRateHz{get;} public double StartThresholdDegPerSec{get;} public double EndThresholdDegPerSec{get;} public double RefractoryPeriodMs{get;} public int PadLengthSamples{get;} public int MinimumFilterableSegmentSamples{get;} public IReadOnlyList<double[]> Sos{get;}
        public static FrozenSubmovementContract From(FrozenCalibrationConfiguration configuration)
        {
            if(configuration==null)throw new ArgumentNullException(nameof(configuration));
            try
            {
                using JsonDocument document=JsonDocument.Parse(configuration.Record.TrackingContractJson);JsonElement pipeline=document.RootElement.GetProperty("signal_pipeline");var sections=new List<double[]>();
                foreach(JsonElement row in pipeline.GetProperty("sos").EnumerateArray()){var values=new List<double>();foreach(JsonElement value in row.EnumerateArray())values.Add(value.GetDouble());if(values.Count!=6||values[3]!=1d)throw new InvalidDataException("Frozen SOS section is invalid.");sections.Add(values.ToArray());}
                double sampling=pipeline.GetProperty("sampling_rate_hz").GetDouble(),start=pipeline.GetProperty("start_threshold_deg_per_sec").GetDouble(),end=pipeline.GetProperty("end_threshold_deg_per_sec").GetDouble(),refractory=pipeline.GetProperty("refractory_period_ms").GetDouble();int pad=pipeline.GetProperty("pad_length_samples").GetInt32(),minimum=pipeline.GetProperty("minimum_filterable_segment_samples").GetInt32();
                if(sections.Count==0||sampling!=configuration.Record.InputSamplingRateHz||start!=configuration.Record.SubmovementStartDegPerSec||end!=configuration.Record.SubmovementEndDegPerSec||refractory!=configuration.Record.RefractoryPeriodMs||end>=start||pad<0||minimum<=pad+1||!string.Equals(pipeline.GetProperty("application").GetString(),"forward-backward-offline",StringComparison.Ordinal)||!string.Equals(pipeline.GetProperty("gap_policy").GetString(),"split-before-filtering-never-bridge",StringComparison.Ordinal)||!string.Equals(pipeline.GetProperty("start_boundary").GetString(),"greater-than-or-equal",StringComparison.Ordinal)||!string.Equals(pipeline.GetProperty("end_boundary").GetString(),"strictly-less-than",StringComparison.Ordinal))throw new InvalidDataException("Frozen Submovement contract is unsupported.");
                return new FrozenSubmovementContract(configuration.Record.SignalPipelineVersion,sampling,start,end,refractory,pad,minimum,new ReadOnlyCollection<double[]>(sections));
            }
            catch(JsonException exception){throw new InvalidDataException("Frozen Submovement contract is invalid.",exception);}
        }
    }

    public sealed class SubmovementEvent
    {
        public SubmovementEvent(int segmentIndex,int onsetSample,int endSample,double onsetSeconds,double endSeconds){SegmentIndex=segmentIndex;OnsetSample=onsetSample;EndSample=endSample;OnsetSeconds=onsetSeconds;EndSeconds=endSeconds;}
        public int SegmentIndex{get;} public int OnsetSample{get;} public int EndSample{get;} public double OnsetSeconds{get;} public double EndSeconds{get;}
    }

    public sealed class SubmovementAnalysisResult
    {
        public SubmovementAnalysisResult(string pipelineVersion,bool signalEligible,string disposition,IReadOnlyList<SubmovementEvent> events)
        { SignalPipelineVersion=pipelineVersion;SignalEligible=signalEligible;Disposition=disposition;Events=new ReadOnlyCollection<SubmovementEvent>(new List<SubmovementEvent>(events??throw new ArgumentNullException(nameof(events)))); }
        public string SignalPipelineVersion{get;} public bool SignalEligible{get;} public string Disposition{get;} public IReadOnlyList<SubmovementEvent> Events{get;} public int? Count=>SignalEligible?Events.Count:(int?)null;
    }

    public sealed class SubmovementSignalProcessor
    {
        private readonly FrozenSubmovementContract contract;
        public SubmovementSignalProcessor(FrozenSubmovementContract contract){this.contract=contract??throw new ArgumentNullException(nameof(contract));}
        public SubmovementAnalysisResult Analyze(IReadOnlyList<UniformAngularSegment> segments,InputTimingDiagnostics diagnostics)
        {
            if(diagnostics==null)throw new ArgumentNullException(nameof(diagnostics));if(!string.Equals(diagnostics.SignalPipelineVersion,contract.SignalPipelineVersion,StringComparison.Ordinal))throw new ArgumentException("Timing and signal pipeline versions must match.",nameof(diagnostics));if(!diagnostics.TimingContractPassed)return new SubmovementAnalysisResult(contract.SignalPipelineVersion,false,"signal-ineligible-timing-contract",Array.Empty<SubmovementEvent>());
            if(segments==null||segments.Count==0)throw new ArgumentException("At least one gap-safe segment is required.",nameof(segments));var events=new List<SubmovementEvent>();
            for(int segmentIndex=0;segmentIndex<segments.Count;segmentIndex++)
            {
                UniformAngularSegment segment=segments[segmentIndex]??throw new ArgumentException("Segments cannot contain null.",nameof(segments));
                if(!segment.FilterEligible||segment.TimeSeconds.Count<contract.MinimumFilterableSegmentSamples)return new SubmovementAnalysisResult(contract.SignalPipelineVersion,false,"signal-ineligible-short-segment",Array.Empty<SubmovementEvent>());
                if(segment.TimeSeconds.Count!=segment.CumulativeAzimuthDeg.Count||segment.TimeSeconds.Count!=segment.CumulativeElevationDeg.Count)throw new ArgumentException("Uniform segment axes must have equal lengths.",nameof(segments));
                double[] azimuth=Filter(segment.CumulativeAzimuthDeg),elevation=Filter(segment.CumulativeElevationDeg),velocity=Velocity(azimuth,elevation);IReadOnlyList<(int onset,int end)> detected=Detect(velocity);
                foreach((int onset,int end) item in detected)events.Add(new SubmovementEvent(segmentIndex,item.onset,item.end,segment.TimeSeconds[item.onset],segment.TimeSeconds[item.end]));
            }
            return new SubmovementAnalysisResult(contract.SignalPipelineVersion,true,"accepted",events);
        }
        public IReadOnlyList<(int onset,int end)> DetectVelocityForVerification(IReadOnlyList<double> velocity)
        { if(velocity==null)throw new ArgumentNullException(nameof(velocity));var values=new double[velocity.Count];for(int i=0;i<values.Length;i++){values[i]=velocity[i];if(double.IsNaN(values[i])||double.IsInfinity(values[i]))throw new ArgumentOutOfRangeException(nameof(velocity));}return Detect(values); }
        private double[] Filter(IReadOnlyList<double> values)
        {
            if(values.Count<=contract.PadLengthSamples+1)throw new ArgumentException("Signal is too short for the frozen odd padding.",nameof(values));int pad=contract.PadLengthSamples;double[] extended=new double[values.Count+2*pad];
            for(int i=0;i<pad;i++)extended[i]=2d*values[0]-values[pad-i];for(int i=0;i<values.Count;i++)extended[pad+i]=values[i];for(int i=0;i<pad;i++)extended[pad+values.Count+i]=2d*values[values.Count-1]-values[values.Count-2-i];
            double[,] zi=SteadyState();double[] forward=Apply(extended,zi,extended[0]);Array.Reverse(forward);double[] backward=Apply(forward,zi,forward[0]);Array.Reverse(backward);double[] result=new double[values.Count];Array.Copy(backward,pad,result,0,result.Length);return result;
        }
        private double[,] SteadyState(){double scale=1d;var states=new double[contract.Sos.Count,2];for(int i=0;i<contract.Sos.Count;i++){double[] row=contract.Sos[i];double gain=(row[0]+row[1]+row[2])/(row[3]+row[4]+row[5]);states[i,0]=scale*(gain-row[0]);states[i,1]=scale*(row[2]-row[5]*gain);scale*=gain;}return states;}
        private double[] Apply(double[] input,double[,] steady,double first){double[] output=(double[])input.Clone();for(int section=0;section<contract.Sos.Count;section++){double[] row=contract.Sos[section];double z0=steady[section,0]*first,z1=steady[section,1]*first;var next=new double[output.Length];for(int sample=0;sample<output.Length;sample++){double value=row[0]*output[sample]+z0;z0=row[1]*output[sample]-row[4]*value+z1;z1=row[2]*output[sample]-row[5]*value;next[sample]=value;}output=next;}return output;}
        private double[] Velocity(double[] azimuth,double[] elevation){var velocity=new double[azimuth.Length];for(int i=1;i<velocity.Length;i++){double x=azimuth[i]-azimuth[i-1],y=elevation[i]-elevation[i-1];velocity[i]=Math.Sqrt(x*x+y*y)*contract.SamplingRateHz;}return velocity;}
        private IReadOnlyList<(int onset,int end)> Detect(double[] velocity)
        {
            var raw=new List<(int onset,int end)>();int? onset=null;for(int i=0;i<velocity.Length;i++){if(!onset.HasValue&&velocity[i]>=contract.StartThresholdDegPerSec)onset=i;else if(onset.HasValue&&velocity[i]<contract.EndThresholdDegPerSec){raw.Add((onset.Value,i));onset=null;}}if(onset.HasValue)raw.Add((onset.Value,velocity.Length-1));
            int refractory=checked((int)Math.Round(contract.RefractoryPeriodMs*contract.SamplingRateHz/1000d));var merged=new List<(int onset,int end)>();foreach((int onset,int end) item in raw){if(merged.Count>0&&item.onset-merged[merged.Count-1].end<refractory){var previous=merged[merged.Count-1];merged[merged.Count-1]=(previous.onset,item.end);}else merged.Add(item);}return merged.AsReadOnly();
        }
    }
}
