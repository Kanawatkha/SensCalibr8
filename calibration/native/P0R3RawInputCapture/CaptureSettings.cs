namespace SensCalibr8.Calibration.Native;

internal static class CaptureSettings
{
    public const string ProtocolId = "sc8-p0-r1-protocol-v1";
    public static readonly string EnvironmentId = "sc8-env-zulartan-wg903-native-v2";
    public const string PilotPlanId = "sc8-p0-r3-input-timing-pilot-v3";
    public static readonly string ConfirmationPlanId = "sc8-p0-r3-input-timing-confirmation-v4";
    public const string ConditionId = "wg903-1600dpi-1000hz-native-wm-input";
    public static readonly string HarnessVersion = "p0-r3-native-harness-v2";
    public const string TimestampSource = "win32-wm-input-qpc";
    public const string MotionInstruction =
        "Continuous figure-eight motion at a comfortable steady pace without lifting the mouse.";
    public const int PlannedRepeatCount = 5;
    public const int TraceDurationSeconds = 30;
    public const int MouseDpi = 1600;
    public const int ConfiguredPollingRateHz = 1000;
    public const int MenuWidth = 960;
    public const int MenuHeight = 540;
    public const int TargetDisplayRefreshRateHz = 144;

    public static string EvidenceRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SC8",
        "P0R3Native");
}
