using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace SensCalibr8.Calibration.Native;

internal static class Program
{
    [STAThread]
    private static int Main(string[] arguments)
    {
        if (arguments.Any(value => string.Equals(value, "--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSelfTest();
        }

        string evidenceState = arguments.Any(value =>
            string.Equals(value, "--p0r3-stage=confirmation", StringComparison.OrdinalIgnoreCase))
            ? "confirmation"
            : "pilot";
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(evidenceState));
        return 0;
    }

    private static int RunSelfTest()
    {
        List<string> failures = new();
        if (Marshal.SizeOf<NativeRawInput.RawInputHeader>() != NativeRawInput.ExpectedHeaderSize)
        {
            failures.Add("RAWINPUTHEADER ABI size mismatch");
        }
        if (Stopwatch.Frequency <= 0)
        {
            failures.Add("QPC/Stopwatch frequency is not positive");
        }
        int firstId = NativeRawInput.StableDeviceId("device-a");
        int secondId = NativeRawInput.StableDeviceId("DEVICE-A");
        if (firstId != secondId || firstId < 0)
        {
            failures.Add("stable native device ID is not deterministic");
        }
        string sample = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["InputEventTimestampSeconds"] = 1.0,
            ["TimestampSource"] = CaptureSettings.TimestampSource
        });
        if (!sample.Contains("win32-wm-input-qpc", StringComparison.Ordinal))
        {
            failures.Add("native timestamp source is not serialized");
        }
        if (CaptureSettings.ConfirmationPlanId !=
            "sc8-p0-r3-input-timing-confirmation-v4" ||
            CaptureSettings.EnvironmentId != "sc8-env-zulartan-wg903-native-v2" ||
            CaptureSettings.HarnessVersion != "p0-r3-native-harness-v2")
        {
            failures.Add("fresh Confirmation v4 identity is not frozen correctly");
        }

        if (failures.Count > 0)
        {
            Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
            return 1;
        }

        Console.WriteLine("P0-R3 native capture self-test passed.");
        return 0;
    }
}
