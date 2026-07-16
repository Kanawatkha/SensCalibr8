using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;

namespace SensCalibr8.TestLogic
{
    public interface IRawMouseInputSource
    {
        event Action<RawMouseInputEvent> DeltaReceived;
        void StartCapture();
        void StopCapture();
    }

    public sealed class InputAngularConverter
    {
        private readonly double degreesPerCount;

        public InputAngularConverter(double testedSensitivity, ResearchConstants constants)
        {
            if (constants == null) throw new ArgumentNullException(nameof(constants));
            if (double.IsNaN(testedSensitivity) || double.IsInfinity(testedSensitivity) || testedSensitivity <= 0d)
                throw new ArgumentOutOfRangeException(nameof(testedSensitivity));
            degreesPerCount = testedSensitivity * constants.ValorantYawMultiplier;
        }

        public double DegreesPerCount => degreesPerCount;
        public double DeltaToDegrees(double rawDelta) => rawDelta * degreesPerCount;
    }

    public sealed class DeterministicInputCapture : IDisposable
    {
        private readonly IRawMouseInputSource source;
        private readonly InputAngularConverter angular;
        private readonly List<CapturedMouseSample> samples = new List<CapturedMouseSample>();
        private double firstMonotonicTimestamp;
        private double cumulativeAzimuth;
        private double cumulativeElevation;
        private bool capturing;
        private bool disposed;

        public DeterministicInputCapture(IRawMouseInputSource source, InputAngularConverter angular)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.angular = angular ?? throw new ArgumentNullException(nameof(angular));
        }

        public bool IsCapturing => capturing;

        public void Start()
        {
            ThrowIfDisposed();
            if (capturing) throw new InvalidOperationException("Input capture is already active.");
            samples.Clear();
            cumulativeAzimuth = 0d;
            cumulativeElevation = 0d;
            firstMonotonicTimestamp = 0d;
            source.DeltaReceived += OnDelta;
            capturing = true;
            try { source.StartCapture(); }
            catch
            {
                capturing = false;
                source.DeltaReceived -= OnDelta;
                throw;
            }
        }

        public IReadOnlyList<CapturedMouseSample> Stop()
        {
            ThrowIfDisposed();
            if (!capturing) throw new InvalidOperationException("Input capture is not active.");
            source.StopCapture();
            source.DeltaReceived -= OnDelta;
            capturing = false;
            return new ReadOnlyCollection<CapturedMouseSample>(new List<CapturedMouseSample>(samples));
        }

        public void Dispose()
        {
            if (disposed) return;
            if (capturing)
            {
                source.StopCapture();
                source.DeltaReceived -= OnDelta;
                capturing = false;
            }
            disposed = true;
        }

        private void OnDelta(RawMouseInputEvent value)
        {
            if (!capturing || value == null) return;
            if (samples.Count == 0) firstMonotonicTimestamp = value.MonotonicTimestampSeconds;
            cumulativeAzimuth += angular.DeltaToDegrees(value.RawDeltaX);
            cumulativeElevation += angular.DeltaToDegrees(value.RawDeltaY);
            samples.Add(new CapturedMouseSample(samples.Count,
                value.MonotonicTimestampSeconds - firstMonotonicTimestamp, value, cumulativeAzimuth, cumulativeElevation));
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(DeterministicInputCapture));
        }
    }
}
