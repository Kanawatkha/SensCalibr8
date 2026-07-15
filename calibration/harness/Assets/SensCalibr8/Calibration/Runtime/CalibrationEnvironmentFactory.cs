using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SensCalibr8.Calibration
{
    public static class CalibrationEnvironmentFactory
    {
        public static CalibrationEnvironmentManifest Capture(
            string protocolId,
            string environmentId,
            CalibrationManualEnvironment manual,
            int targetMouseDeviceId = InputDevice.InvalidDeviceId)
        {
            CalibrationManualEnvironment normalizedManual = NormalizeManual(manual);
            Mouse mouse = InputSystem.GetDeviceById(targetMouseDeviceId) as Mouse;
            if (mouse == null)
            {
                mouse = Mouse.current;
            }
            string executablePath = CaptureExecutablePath();
            return new CalibrationEnvironmentManifest
            {
                ProtocolId = CalibrationIdFactory.NormalizeToken(protocolId),
                EnvironmentId = CalibrationIdFactory.NormalizeToken(environmentId),
                CapturedUtc = DateTime.UtcNow.ToString("o"),
                HarnessVersion = CalibrationHarnessMetadata.HarnessVersion,
                HarnessChecksum = CaptureHarnessChecksum(executablePath),
                RuntimeBuildType = Application.isEditor ? "unity-editor" : "windows-standalone",
                ExecutableName = ValueOrUnknown(Path.GetFileName(executablePath)),
                ExecutableChecksum = CaptureFileChecksum(executablePath),
                UnityVersion = Application.unityVersion,
                InputSystemVersion = InputSystem.version.ToString(),
                InputUpdateMode = InputSystem.settings.updateMode.ToString(),
                TimestampSource = "unity-input-event-time",
                RedundantEventMergingDisabled = InputSystem.settings.disableRedundantEventsMerging,
                DedicatedRawInputMessagePump = false,
                OperatingSystem = SystemInfo.operatingSystem,
                DeviceModel = SystemInfo.deviceModel,
                ProcessorType = SystemInfo.processorType,
                ProcessorCount = SystemInfo.processorCount,
                SystemMemoryMb = SystemInfo.systemMemorySize,
                GraphicsDeviceName = SystemInfo.graphicsDeviceName,
                GraphicsDeviceVersion = SystemInfo.graphicsDeviceVersion,
                ActiveWidth = Screen.width,
                ActiveHeight = Screen.height,
                ActiveRefreshRateHz = Screen.currentResolution.refreshRateRatio.value,
                FullScreenMode = Screen.fullScreenMode.ToString(),
                ApplicationFocused = Application.isFocused,
                MouseLayout = mouse == null ? CalibrationHarnessMetadata.UnknownValue : mouse.layout,
                MouseDeviceId = mouse == null ? InputDevice.InvalidDeviceId : mouse.deviceId,
                MouseInterface = ValueOrUnknown(mouse == null ? null : mouse.description.interfaceName),
                MouseProduct = ValueOrUnknown(mouse == null ? null : mouse.description.product),
                MouseManufacturer = ValueOrUnknown(mouse == null ? null : mouse.description.manufacturer),
                MouseVersion = ValueOrUnknown(mouse == null ? null : mouse.description.version),
                Manual = normalizedManual
            };
        }

        private static CalibrationManualEnvironment NormalizeManual(CalibrationManualEnvironment manual)
        {
            CalibrationManualEnvironment source = manual ?? new CalibrationManualEnvironment();
            return new CalibrationManualEnvironment
            {
                DisplayModel = ValueOrUnknown(source.DisplayModel),
                NativeResolution = ValueOrUnknown(source.NativeResolution),
                DisplayRefreshRate = ValueOrUnknown(source.DisplayRefreshRate),
                DisplayScaling = ValueOrUnknown(source.DisplayScaling),
                VSyncState = ValueOrUnknown(source.VSyncState),
                AdaptiveSyncState = ValueOrUnknown(source.AdaptiveSyncState),
                MouseManufacturer = ValueOrUnknown(source.MouseManufacturer),
                MouseModel = ValueOrUnknown(source.MouseModel),
                MouseConnection = ValueOrUnknown(source.MouseConnection),
                MouseFirmware = ValueOrUnknown(source.MouseFirmware),
                MouseDpi = ValueOrUnknown(source.MouseDpi),
                MouseDpiEvidenceSource = ValueOrUnknown(source.MouseDpiEvidenceSource),
                ConfiguredPollingRate = ValueOrUnknown(source.ConfiguredPollingRate),
                PollingRateEvidenceSource = ValueOrUnknown(source.PollingRateEvidenceSource),
                UsbPathOrHub = ValueOrUnknown(source.UsbPathOrHub),
                MousePowerState = ValueOrUnknown(source.MousePowerState),
                PointerSpeed = ValueOrUnknown(source.PointerSpeed),
                PointerAccelerationState = ValueOrUnknown(source.PointerAccelerationState),
                MousepadDescription = ValueOrUnknown(source.MousepadDescription),
                OperatorId = ValueOrUnknown(source.OperatorId),
                DominantHand = ValueOrUnknown(source.DominantHand),
                GripDescriptor = ValueOrUnknown(source.GripDescriptor),
                MovementDescriptor = ValueOrUnknown(source.MovementDescriptor),
                PostureNotes = ValueOrUnknown(source.PostureNotes),
                WarmupProcedure = ValueOrUnknown(source.WarmupProcedure),
                PowerPlan = ValueOrUnknown(source.PowerPlan),
                BackgroundLoadPolicy = ValueOrUnknown(source.BackgroundLoadPolicy),
                ThermalPowerNotes = ValueOrUnknown(source.ThermalPowerNotes),
                NetworkOfflineState = ValueOrUnknown(source.NetworkOfflineState)
            };
        }

        private static string CaptureHarnessChecksum(string executablePath)
        {
            string assemblyPath = typeof(CalibrationHarnessMetadata).Assembly.Location;
            if (!string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(assemblyPath))
            {
                return CalibrationFileSystem.ComputeSha256(assemblyPath);
            }

            return CaptureFileChecksum(executablePath);
        }

        private static string CaptureExecutablePath()
        {
            try
            {
                using (Process process = Process.GetCurrentProcess())
                {
                    return process.MainModule == null ? null : process.MainModule.FileName;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string CaptureFileChecksum(string path)
        {
            return string.IsNullOrWhiteSpace(path) || !File.Exists(path)
                ? CalibrationHarnessMetadata.UnknownValue
                : CalibrationFileSystem.ComputeSha256(path);
        }

        private static string ValueOrUnknown(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? CalibrationHarnessMetadata.UnknownValue : value.Trim();
        }
    }
}
