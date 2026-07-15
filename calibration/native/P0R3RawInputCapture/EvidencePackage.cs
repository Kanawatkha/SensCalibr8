using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SensCalibr8.Calibration.Native;

internal sealed class EvidencePackage : IDisposable
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions CompactJson = new();
    private readonly StreamWriter rawWriter;
    private readonly StreamWriter frameWriter;
    private readonly StreamWriter targetWriter;
    private readonly string integrityPath;
    private readonly string finalPath;
    private readonly string harnessChecksum;
    private readonly string evidenceState;
    private readonly string startedUtc;
    private long rawSequence;
    private long frameSequence;
    private long targetSequence;
    private bool finalized;

    public EvidencePackage(
        int repetitionOrdinal,
        string evidenceState,
        int deviceId,
        string devicePath,
        bool applicationFocused)
    {
        this.evidenceState = evidenceState;
        RunId = CreateRunId(DateTime.UtcNow, repetitionOrdinal);
        startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        TraceId = RunId + "-mouse";
        RunDirectory = Path.Combine(CaptureSettings.EvidenceRoot, RunId);
        Directory.CreateDirectory(RunDirectory);

        string assemblyPath = Assembly.GetExecutingAssembly().Location;
        string executablePath = Environment.ProcessPath ?? assemblyPath;
        harnessChecksum = ComputeSha256(assemblyPath);
        string executableChecksum = ComputeSha256(executablePath);
        string planId = evidenceState == "confirmation"
            ? CaptureSettings.ConfirmationPlanId
            : CaptureSettings.PilotPlanId;

        string environmentPath = ArtifactPath("environment-manifest", "json");
        string capturePlanPath = ArtifactPath("capture-plan", "json");
        string startedPath = ArtifactPath("run-started", "json");
        finalPath = ArtifactPath("run-final", "json");
        integrityPath = ArtifactPath("integrity-manifest", "json");
        string rawPath = ArtifactPath("raw-mouse-events", "jsonl");
        string framePath = ArtifactPath("frame-timing", "jsonl");
        string targetPath = ArtifactPath("target-camera-events", "jsonl");

        ValidateLegacyWindowsPathCapacity(new[]
        {
            environmentPath, capturePlanPath, startedPath, finalPath,
            integrityPath, rawPath, framePath, targetPath
        });

        WriteNewJson(environmentPath, BuildEnvironment(
            deviceId,
            devicePath,
            applicationFocused,
            assemblyPath,
            executablePath,
            executableChecksum));
        WriteNewJson(capturePlanPath, BuildCapturePlan(planId, repetitionOrdinal));
        WriteNewJson(startedPath, BuildRunManifest(
            planId,
            "started",
            "unknown",
            startedUtc,
            "unknown"));

        rawWriter = CreateNewWriter(rawPath);
        frameWriter = CreateNewWriter(framePath);
        targetWriter = CreateNewWriter(targetPath);
        RecordTargetEvent("native-capture-start");
    }

    public string RunId { get; }
    public string TraceId { get; }
    public string RunDirectory { get; }

    public void RecordRaw(long timestampTicks, int deviceId, string devicePath, NativeRawInput.MouseMessage message)
    {
        EnsureOpen();
        double seconds = timestampTicks / (double)Stopwatch.Frequency;
        WriteCompact(rawWriter, new Dictionary<string, object?>
        {
            ["TraceId"] = TraceId,
            ["RunId"] = RunId,
            ["Sequence"] = rawSequence++,
            ["MonotonicTimestampTicks"] = timestampTicks,
            ["MonotonicTimestampSeconds"] = seconds,
            ["InputEventTimestampSeconds"] = seconds,
            ["DeviceId"] = deviceId,
            ["DeviceLayout"] = "RawMouse",
            ["DeviceInterface"] = "Win32RawInput",
            ["DeviceProduct"] = devicePath,
            ["DeviceManufacturer"] = "SIGNO-user-confirmed",
            ["DeviceVersion"] = "unknown",
            ["EventType"] = "WM_INPUT",
            ["RawDeltaX"] = message.DeltaX,
            ["RawDeltaY"] = message.DeltaY,
            ["NativeMouseFlags"] = message.Flags,
            ["NativeButtonFlags"] = message.ButtonFlags,
            ["TimestampSource"] = CaptureSettings.TimestampSource
        });
    }

    public void RecordHeartbeat(long timestampTicks, double elapsedSeconds, bool applicationFocused)
    {
        EnsureOpen();
        WriteCompact(frameWriter, new Dictionary<string, object?>
        {
            ["RunId"] = RunId,
            ["EnvironmentId"] = CaptureSettings.EnvironmentId,
            ["Sequence"] = frameSequence,
            ["MonotonicTimestampTicks"] = timestampTicks,
            ["MonotonicTimestampSeconds"] = timestampTicks / (double)Stopwatch.Frequency,
            ["UnityFrameIndex"] = checked((int)frameSequence++),
            ["UnscaledDeltaTimeSeconds"] = elapsedSeconds,
            ["ApplicationFocused"] = applicationFocused,
            ["ScreenWidth"] = Screen.PrimaryScreen?.Bounds.Width ?? CaptureSettings.MenuWidth,
            ["ScreenHeight"] = Screen.PrimaryScreen?.Bounds.Height ?? CaptureSettings.MenuHeight,
            ["RefreshRateHz"] = CaptureSettings.TargetDisplayRefreshRateHz,
            ["FullScreenMode"] = "NativeBorderlessFullscreen",
            ["TimingSource"] = "native-ui-heartbeat-qpc"
        });
    }

    public void Complete(string status, string reason)
    {
        if (finalized)
        {
            throw new InvalidOperationException("The evidence package is already finalized.");
        }

        RecordTargetEvent("native-capture-end");
        DisposeWriters();
        string planId = evidenceState == "confirmation"
            ? CaptureSettings.ConfirmationPlanId
            : CaptureSettings.PilotPlanId;
        WriteNewJson(finalPath, BuildRunManifest(
            planId,
            status,
            reason,
            startedUtc,
            DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
        WriteIntegrityManifest();
        finalized = true;
    }

    public void Dispose()
    {
        if (!finalized)
        {
            Complete("interrupted", "disposed-without-completion");
        }
    }

    private Dictionary<string, object?> BuildEnvironment(
        int deviceId,
        string devicePath,
        bool applicationFocused,
        string assemblyPath,
        string executablePath,
        string executableChecksum)
    {
        Rectangle screen = Screen.PrimaryScreen?.Bounds ??
            new Rectangle(0, 0, CaptureSettings.MenuWidth, CaptureSettings.MenuHeight);
        return new Dictionary<string, object?>
        {
            ["ProtocolId"] = CaptureSettings.ProtocolId,
            ["EnvironmentId"] = CaptureSettings.EnvironmentId,
            ["CapturedUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            ["HarnessVersion"] = CaptureSettings.HarnessVersion,
            ["HarnessChecksum"] = harnessChecksum,
            ["RuntimeBuildType"] = "windows-standalone",
            ["ExecutableName"] = Path.GetFileName(executablePath),
            ["ExecutableChecksum"] = executableChecksum,
            ["RuntimeAssemblyName"] = Path.GetFileName(assemblyPath),
            ["UnityVersion"] = "not-applicable-native-capture-helper",
            ["InputSystemVersion"] = "not-applicable-native-win32-raw-input",
            ["InputUpdateMode"] = "dedicated-win32-message-pump",
            ["RedundantEventMergingDisabled"] = true,
            ["DedicatedRawInputMessagePump"] = true,
            ["TimestampSource"] = CaptureSettings.TimestampSource,
            ["OperatingSystem"] = Environment.OSVersion.VersionString,
            ["DeviceModel"] = "Acer Nitro AN515-57",
            ["ProcessorType"] = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown",
            ["ProcessorCount"] = Environment.ProcessorCount,
            ["SystemMemoryMb"] = 0,
            ["GraphicsDeviceName"] = "NVIDIA GeForce RTX 3050 Laptop GPU",
            ["GraphicsDeviceVersion"] = "32.0.15.9174",
            ["ActiveWidth"] = screen.Width,
            ["ActiveHeight"] = screen.Height,
            ["ActiveRefreshRateHz"] = CaptureSettings.TargetDisplayRefreshRateHz,
            ["FullScreenMode"] = "NativeBorderlessFullscreen",
            ["ApplicationFocused"] = applicationFocused,
            ["MouseLayout"] = "RawMouse",
            ["MouseDeviceId"] = deviceId,
            ["MouseInterface"] = "Win32RawInput",
            ["MouseProduct"] = devicePath,
            ["MouseManufacturer"] = "SIGNO-user-confirmed",
            ["MouseVersion"] = "unknown",
            ["Manual"] = BuildManualEnvironment()
        };
    }

    private Dictionary<string, object?> BuildCapturePlan(string planId, int repetitionOrdinal)
    {
        string controlledVariables = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["mouse_dpi"] = CaptureSettings.MouseDpi,
            ["configured_polling_rate_hz"] = CaptureSettings.ConfiguredPollingRateHz,
            ["timestamp_source"] = CaptureSettings.TimestampSource,
            ["dedicated_raw_input_message_pump"] = true,
            ["display_mode"] = "NativeBorderlessFullscreen",
            ["display_refresh_rate_hz"] = CaptureSettings.TargetDisplayRefreshRateHz,
            ["vsync"] = "not-applicable-native-helper",
            ["adaptive_sync"] = "operator-confirmed-off",
            ["network"] = "offline"
        });
        return new Dictionary<string, object?>
        {
            ["ProtocolId"] = CaptureSettings.ProtocolId,
            ["CapturePlanId"] = planId,
            ["EnvironmentId"] = CaptureSettings.EnvironmentId,
            ["ConditionId"] = CaptureSettings.ConditionId,
            ["PlannedRepeatCount"] = CaptureSettings.PlannedRepeatCount,
            ["RepetitionOrdinal"] = repetitionOrdinal,
            ["ExecutionOrder"] = "sequential-repetitions-1-through-5-single-condition",
            ["ControlledVariablesJson"] = controlledVariables,
            ["EvidenceState"] = evidenceState,
            ["AcceptanceOwner"] = "project-owner",
            ["ControlledMotionInstruction"] = CaptureSettings.MotionInstruction,
            ["TraceDurationSeconds"] = CaptureSettings.TraceDurationSeconds,
            ["CreatedUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
        };
    }

    private Dictionary<string, object?> BuildRunManifest(
        string planId,
        string status,
        string reason,
        string startedUtc,
        string endedUtc)
    {
        return new Dictionary<string, object?>
        {
            ["ProtocolId"] = CaptureSettings.ProtocolId,
            ["EnvironmentId"] = CaptureSettings.EnvironmentId,
            ["CapturePlanId"] = planId,
            ["ConditionId"] = CaptureSettings.ConditionId,
            ["RunId"] = RunId,
            ["TraceId"] = TraceId,
            ["HarnessVersion"] = CaptureSettings.HarnessVersion,
            ["HarnessChecksum"] = harnessChecksum,
            ["Status"] = status,
            ["Reason"] = reason,
            ["StartedUtc"] = startedUtc,
            ["EndedUtc"] = endedUtc,
            ["StopwatchFrequency"] = Stopwatch.Frequency,
            ["TimestampSource"] = CaptureSettings.TimestampSource
        };
    }

    private static Dictionary<string, object?> BuildManualEnvironment()
    {
        const string Known = "operator-confirmed";
        return new Dictionary<string, object?>
        {
            ["DisplayModel"] = "NCP004D",
            ["NativeResolution"] = "1920x1080",
            ["DisplayRefreshRate"] = "144 Hz",
            ["DisplayScaling"] = "125% host observation",
            ["VSyncState"] = "not-applicable-native-helper",
            ["AdaptiveSyncState"] = "operator-confirmed-off",
            ["MouseManufacturer"] = "SIGNO",
            ["MouseModel"] = "WG-903",
            ["MouseConnection"] = "user-reported Bluetooth; host-observed USB HID VID_1D57 PID_FA60",
            ["MouseFirmware"] = "unknown",
            ["MouseDpi"] = CaptureSettings.MouseDpi.ToString(CultureInfo.InvariantCulture),
            ["MouseDpiEvidenceSource"] = "SIGNO software screenshot and project-owner confirmation 2026-07-15",
            ["ConfiguredPollingRate"] = CaptureSettings.ConfiguredPollingRateHz + " Hz",
            ["PollingRateEvidenceSource"] = "SIGNO software and project-owner confirmation 2026-07-15; measured cadence authoritative",
            ["UsbPathOrHub"] = "captured-native-raw-device-path",
            ["MousePowerState"] = Known + "-stable",
            ["PointerSpeed"] = "Windows registry MouseSensitivity=10",
            ["PointerAccelerationState"] = "audit-only registry MouseSpeed=1 Threshold1=6 Threshold2=10",
            ["MousepadDescription"] = "not-required-for-p0-r3-input-timing",
            ["OperatorId"] = "local-primary-operator",
            ["DominantHand"] = "not-required-for-p0-r3-input-timing",
            ["GripDescriptor"] = "not-required-for-p0-r3-input-timing",
            ["MovementDescriptor"] = "controlled-figure-eight",
            ["PostureNotes"] = "audit-only-not-supplied",
            ["WarmupProcedure"] = "operator-ready-confirmation",
            ["PowerPlan"] = "High performance; operator-confirmed-AC-power",
            ["BackgroundLoadPolicy"] = "operator-confirmed-no-material-background-load",
            ["ThermalPowerNotes"] = "operator-confirmed-no-known-anomaly",
            ["NetworkOfflineState"] = "offline"
        };
    }

    private void RecordTargetEvent(string eventType)
    {
        long ticks = Stopwatch.GetTimestamp();
        WriteCompact(targetWriter, new Dictionary<string, object?>
        {
            ["RunId"] = RunId,
            ["ConditionId"] = CaptureSettings.ConditionId,
            ["Sequence"] = targetSequence++,
            ["MonotonicTimestampTicks"] = ticks,
            ["MonotonicTimestampSeconds"] = ticks / (double)Stopwatch.Frequency,
            ["EventType"] = eventType,
            ["TargetId"] = "not-applicable-p0-r3-native-input",
            ["TargetPositionX"] = 0,
            ["TargetPositionY"] = 0,
            ["TargetPositionZ"] = 0,
            ["CameraAzimuthDegrees"] = 0,
            ["CameraElevationDegrees"] = 0
        });
    }

    private void WriteIntegrityManifest()
    {
        List<Dictionary<string, object?>> artifacts = new();
        foreach (string file in Directory.GetFiles(RunDirectory).OrderBy(value => value, StringComparer.Ordinal))
        {
            if (string.Equals(file, integrityPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FileInfo info = new(file);
            artifacts.Add(new Dictionary<string, object?>
            {
                ["ArtifactId"] = Path.GetFileNameWithoutExtension(file),
                ["RelativePath"] = Path.GetFileName(file),
                ["ByteSize"] = info.Length,
                ["Sha256"] = ComputeSha256(file),
                ["CreatedUtc"] = info.CreationTimeUtc.ToString("o", CultureInfo.InvariantCulture),
                ["ProducerVersion"] = CaptureSettings.HarnessVersion,
                ["ProducerChecksum"] = harnessChecksum,
                ["ProtocolId"] = CaptureSettings.ProtocolId,
                ["EnvironmentId"] = CaptureSettings.EnvironmentId,
                ["CapturePlanId"] = evidenceState == "confirmation"
                    ? CaptureSettings.ConfirmationPlanId
                    : CaptureSettings.PilotPlanId,
                ["ConditionId"] = CaptureSettings.ConditionId,
                ["RunId"] = RunId,
                ["TraceId"] = TraceId
            });
        }

        WriteNewJson(integrityPath, new Dictionary<string, object?>
        {
            ["ProtocolId"] = CaptureSettings.ProtocolId,
            ["EnvironmentId"] = CaptureSettings.EnvironmentId,
            ["CapturePlanId"] = evidenceState == "confirmation"
                ? CaptureSettings.ConfirmationPlanId
                : CaptureSettings.PilotPlanId,
            ["ConditionId"] = CaptureSettings.ConditionId,
            ["RunId"] = RunId,
            ["TraceId"] = TraceId,
            ["ProducerVersion"] = CaptureSettings.HarnessVersion,
            ["ProducerChecksum"] = harnessChecksum,
            ["CreatedUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            ["Artifacts"] = artifacts
        });
    }

    private string ArtifactPath(string artifactType, string extension)
    {
        string fileName = string.Join("_",
            "sc8",
            CaptureSettings.ProtocolId,
            CaptureSettings.EnvironmentId,
            CaptureSettings.ConditionId,
            RunId,
            artifactType) + "." + extension;
        return Path.Combine(RunDirectory, fileName);
    }

    private static string CreateRunId(DateTime utc, int ordinal) =>
        utc.ToString("yyyyMMddTHHmmssfffffffZ", CultureInfo.InvariantCulture).ToLowerInvariant() +
        "-r" + ordinal.ToString(CultureInfo.InvariantCulture);

    private static StreamWriter CreateNewWriter(string path) => new(
        new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
        new UTF8Encoding(false),
        bufferSize: 65536);

    private static void WriteCompact(StreamWriter writer, object value) =>
        writer.WriteLine(JsonSerializer.Serialize(value, CompactJson));

    private static void WriteNewJson(string path, object value)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, value, IndentedJson);
        stream.Flush(true);
    }

    private static string ComputeSha256(string path)
    {
        using SHA256 sha = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private static void ValidateLegacyWindowsPathCapacity(IEnumerable<string> paths)
    {
        string? tooLong = paths.FirstOrDefault(path => Path.GetFullPath(path).Length >= 260);
        if (tooLong is not null)
        {
            throw new PathTooLongException("Evidence path exceeds the target Windows path contract: " + tooLong);
        }
    }

    private void DisposeWriters()
    {
        rawWriter.Flush();
        frameWriter.Flush();
        targetWriter.Flush();
        rawWriter.Dispose();
        frameWriter.Dispose();
        targetWriter.Dispose();
    }

    private void EnsureOpen()
    {
        if (finalized)
        {
            throw new InvalidOperationException("Cannot append to finalized evidence.");
        }
    }
}
