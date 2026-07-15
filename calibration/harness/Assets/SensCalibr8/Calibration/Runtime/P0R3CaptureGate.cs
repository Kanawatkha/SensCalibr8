using System;
using System.Collections.Generic;

namespace SensCalibr8.Calibration
{
    public static class P0R3CaptureGate
    {
        public static void ValidatePlan(
            string protocolId,
            string environmentId,
            string capturePlanId,
            string conditionId,
            int plannedRepeatCount,
            int repetitionOrdinal,
            double traceDurationSeconds,
            string evidenceState,
            string acceptanceOwner,
            string executionOrder,
            string controlledMotionInstruction,
            string controlledVariablesJson,
            CalibrationManualEnvironment manual)
        {
            CalibrationIdFactory.NormalizeToken(protocolId);
            CalibrationIdFactory.NormalizeToken(environmentId);
            CalibrationIdFactory.NormalizeToken(capturePlanId);
            CalibrationIdFactory.NormalizeToken(conditionId);

            if (plannedRepeatCount < 2 || repetitionOrdinal <= 0 ||
                repetitionOrdinal > plannedRepeatCount)
            {
                throw new InvalidOperationException(
                    "P0-R3 requires at least two predeclared independent runs and a valid repetition ordinal.");
            }

            if (double.IsNaN(traceDurationSeconds) || double.IsInfinity(traceDurationSeconds) ||
                traceDurationSeconds <= 0)
            {
                throw new InvalidOperationException(
                    "P0-R3 trace duration must be finite, positive, and frozen before capture.");
            }

            if (!string.Equals(evidenceState, "pilot", StringComparison.Ordinal) &&
                !string.Equals(evidenceState, "confirmation", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "P0-R3 evidence state must be exactly pilot or confirmation.");
            }

            RequireKnown("acceptance owner", acceptanceOwner);
            RequireKnown("execution order", executionOrder);
            RequireKnown("controlled motion instruction", controlledMotionInstruction);
            RequireKnown("controlled variables", controlledVariablesJson);
            ValidateManualEnvironment(manual);
        }

        public static void ValidateManualEnvironment(CalibrationManualEnvironment manual)
        {
            if (manual == null)
            {
                throw new InvalidOperationException(
                    "P0-R3 acceptance capture requires a complete manual environment manifest.");
            }

            Dictionary<string, string> required = new Dictionary<string, string>
            {
                { "VSync state", manual.VSyncState },
                { "adaptive-sync state", manual.AdaptiveSyncState },
                { "mouse connection", manual.MouseConnection },
                { "mouse DPI", manual.MouseDpi },
                { "mouse DPI evidence", manual.MouseDpiEvidenceSource },
                { "configured polling rate", manual.ConfiguredPollingRate },
                { "polling-rate evidence", manual.PollingRateEvidenceSource },
                { "mouse power state", manual.MousePowerState },
                { "operator ID", manual.OperatorId },
                { "power plan", manual.PowerPlan },
                { "background-load policy", manual.BackgroundLoadPolicy },
                { "thermal/power notes", manual.ThermalPowerNotes },
                { "network/offline state", manual.NetworkOfflineState }
            };

            foreach (KeyValuePair<string, string> item in required)
            {
                RequireKnown(item.Key, item.Value);
            }
        }

        public static void ValidateInputCaptureRuntime(bool redundantEventMergingDisabled)
        {
            if (!redundantEventMergingDisabled)
            {
                throw new InvalidOperationException(
                    "P0-R3 raw cadence capture requires Unity mouse-event merging to be disabled.");
            }
        }

        private static void RequireKnown(string fieldName, string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                string.Equals(value.Trim(), CalibrationHarnessMetadata.UnknownValue, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "P0-R3 field is unresolved and blocks capture: " + fieldName + ".");
            }
        }
    }
}
