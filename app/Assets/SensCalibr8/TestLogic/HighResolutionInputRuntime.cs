using System;
using System.Diagnostics;
using SensCalibr8.Core.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SensCalibr8.TestLogic
{
    public interface IHighResolutionClock
    {
        long Frequency { get; }
        long TimestampTicks { get; }
        double TimestampSeconds { get; }
    }

    public sealed class StopwatchHighResolutionClock : IHighResolutionClock
    {
        public long Frequency => Stopwatch.Frequency;
        public long TimestampTicks => Stopwatch.GetTimestamp();
        public double TimestampSeconds => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    }

    public sealed class UnityRawMouseInputSource : MonoBehaviour, IRawMouseInputSource
    {
        private IHighResolutionClock clock;
        private int targetMouseDeviceId;
        private bool capturing;

        public event Action<RawMouseInputEvent> DeltaReceived;

        public void Configure(int mouseDeviceId, IHighResolutionClock highResolutionClock = null)
        {
            if (!(InputSystem.GetDeviceById(mouseDeviceId) is Mouse))
                throw new ArgumentException("A valid Input System mouse device is required.", nameof(mouseDeviceId));
            targetMouseDeviceId = mouseDeviceId;
            clock = highResolutionClock ?? new StopwatchHighResolutionClock();
        }

        public void StartCapture()
        {
            if (capturing) throw new InvalidOperationException("Raw mouse source is already capturing.");
            if (clock == null || !(InputSystem.GetDeviceById(targetMouseDeviceId) is Mouse))
                throw new InvalidOperationException("Raw mouse source must be configured before capture.");
            InputSystem.settings.disableRedundantEventsMerging = true;
            InputSystem.onEvent += OnInputEvent;
            capturing = true;
        }

        public void StopCapture()
        {
            if (!capturing) return;
            InputSystem.onEvent -= OnInputEvent;
            capturing = false;
        }

        private void OnDisable() => StopCapture();

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!capturing || !(device is Mouse mouse) || mouse.deviceId != targetMouseDeviceId ||
                (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())) return;
            if (!mouse.delta.ReadValueFromEvent(eventPtr, out Vector2 delta)) return;
            long ticks = clock.TimestampTicks;
            double seconds = ticks / (double)clock.Frequency;
            DeltaReceived?.Invoke(new RawMouseInputEvent(ticks, seconds, eventPtr.time, delta.x, delta.y,
                mouse.deviceId + ":" + mouse.layout + ":" + ValueOrUnknown(mouse.description.product)));
        }

        private static string ValueOrUnknown(string value) => string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }

    public sealed class UnityFramePolicyScope : IDisposable
    {
        private readonly int previousTargetFrameRate;
        private readonly int previousVSyncCount;
        private bool disposed;

        public UnityFramePolicyScope(FrozenFramePolicy policy, bool adaptiveSyncConfirmedOff)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (policy.AdaptiveSyncRequiredOff && !adaptiveSyncConfirmedOff)
                throw new InvalidOperationException("Adaptive sync must be confirmed off before an acceptance-bearing session.");
            previousTargetFrameRate = Application.targetFrameRate;
            previousVSyncCount = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = policy.VSyncCount;
            Application.targetFrameRate = policy.TargetFrameRateHz;
        }

        public void Dispose()
        {
            if (disposed) return;
            Application.targetFrameRate = previousTargetFrameRate;
            QualitySettings.vSyncCount = previousVSyncCount;
            disposed = true;
        }
    }
}
