using System.Diagnostics;
using System.Globalization;

namespace SensCalibr8.Calibration.Native;

internal sealed class MainForm : Form
{
    private sealed class DeviceActivity
    {
        public long EventCount;
        public long AbsoluteMovement;
        public string Path = "unknown-native-device";
    }

    private readonly string evidenceState;
    private readonly Dictionary<IntPtr, DeviceActivity> detectedDevices = new();
    private readonly Panel preflightPanel = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly Label statusLabel = new() { AutoSize = true, MaximumSize = new Size(880, 0) };
    private readonly Label devicesLabel = new() { AutoSize = true, MaximumSize = new Size(880, 0) };
    private readonly Label captureLabel = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font(FontFamily.GenericSansSerif, 20, FontStyle.Bold),
        Visible = false
    };
    private readonly CheckBox adaptiveSync = new() { AutoSize = true, Text = "I confirmed G-SYNC / FreeSync / adaptive sync is OFF." };
    private readonly CheckBox mousePower = new() { AutoSize = true, Text = "The wireless mouse has stable power for all five runs." };
    private readonly CheckBox backgroundLoad = new() { AutoSize = true, Text = "No material download, recording, overlay, or heavy workload is active." };
    private readonly CheckBox thermalPower = new() { AutoSize = true, Text = "Laptop is plugged in on High performance with no known thermal/power anomaly." };
    private readonly CheckBox offline = new() { AutoSize = true, Text = "The capture is offline; Bluetooth remains enabled." };
    private readonly System.Windows.Forms.Timer uiTimer = new();
    private IntPtr selectedDevice;
    private string selectedDevicePath = "unknown-native-device";
    private int selectedDeviceId;
    private int repetitionOrdinal = 1;
    private bool detectionActive;
    private bool fullscreenReady;
    private bool capturing;
    private long captureStartTicks;
    private long previousHeartbeatTicks;
    private long offTargetNonzeroEvents;
    private EvidencePackage? evidence;

    public MainForm(string evidenceState)
    {
        this.evidenceState = evidenceState;
        Text = "SensCalibr8 P0-R3 Native Raw Input Calibration";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(CaptureSettings.MenuWidth, CaptureSettings.MenuHeight);
        MinimumSize = new Size(CaptureSettings.MenuWidth, CaptureSettings.MenuHeight);
        KeyPreview = true;

        Controls.Add(captureLabel);
        Controls.Add(preflightPanel);
        BuildPreflightUi();

        uiTimer.Interval = Math.Max(
            1,
            (int)Math.Round(1000.0 / CaptureSettings.TargetDisplayRefreshRateHz));
        uiTimer.Tick += OnUiTimer;
        uiTimer.Start();
        Deactivate += (_, _) =>
        {
            if (capturing)
            {
                FinalizeCapture("interrupted", "application-focus-lost");
            }
        };
        FormClosing += OnFormClosing;
    }

    protected override void OnHandleCreated(EventArgs eventArgs)
    {
        base.OnHandleCreated(eventArgs);
        NativeRawInput.RegisterMouse(Handle);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeRawInput.WmInput)
        {
            long timestampTicks = Stopwatch.GetTimestamp();
            if (NativeRawInput.TryReadMouse(message.LParam, out NativeRawInput.MouseMessage raw))
            {
                ProcessRawMouse(timestampTicks, raw);
            }
        }

        base.WndProc(ref message);
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            if (capturing)
            {
                FinalizeCapture("interrupted", "operator-escape");
                return true;
            }

            if (fullscreenReady)
            {
                ReturnToWindowed();
                return true;
            }
        }

        return base.ProcessCmdKey(ref message, keyData);
    }

    private void BuildPreflightUi()
    {
        FlowLayoutPanel flow = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(20)
        };
        preflightPanel.Controls.Add(flow);
        flow.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new Font(FontFamily.GenericSansSerif, 15, FontStyle.Bold),
            Text = "SensCalibr8 — P0-R3 Native WM_INPUT + QPC Capture"
        });
        flow.Controls.Add(new Label { AutoSize = true, Text = "Fresh evidence capture • " + evidenceState.ToUpperInvariant() });
        flow.Controls.Add(new Label { AutoSize = true, Text = "Plan: 5 independent runs × 30 seconds" });
        flow.Controls.Add(new Label { AutoSize = true, Text = "Motion: " + CaptureSettings.MotionInstruction, MaximumSize = new Size(880, 0) });
        flow.Controls.Add(new Label { AutoSize = true, Text = "DPI 1600 • configured polling 1000 Hz • timestamp source: dedicated WM_INPUT message pump + QPC" });
        flow.Controls.Add(statusLabel);
        statusLabel.Text = "Step 1: reset activity, move only the SIGNO mouse, then select the most active native device.";

        Button resetDetection = new() { AutoSize = true, Text = "1. Reset native-device activity" };
        resetDetection.Click += (_, _) =>
        {
            detectedDevices.Clear();
            selectedDevice = IntPtr.Zero;
            selectedDevicePath = "unknown-native-device";
            detectionActive = true;
            devicesLabel.Text = "Detection active. Move only the SIGNO mouse continuously, then click step 2.";
            UpdateStartAvailability();
        };
        flow.Controls.Add(resetDetection);

        Button chooseDevice = new() { AutoSize = true, Text = "2. Use most active native mouse device" };
        chooseDevice.Click += (_, _) => SelectMostActiveDevice();
        flow.Controls.Add(chooseDevice);
        flow.Controls.Add(devicesLabel);
        flow.Controls.Add(adaptiveSync);
        flow.Controls.Add(mousePower);
        flow.Controls.Add(backgroundLoad);
        flow.Controls.Add(thermalPower);
        flow.Controls.Add(offline);

        Button enterFullscreen = new() { AutoSize = true, Text = "Enter native borderless fullscreen" };
        enterFullscreen.Click += (_, _) =>
        {
            if (!PreflightComplete())
            {
                statusLabel.Text = "Complete device selection and every truthful preflight confirmation first.";
                return;
            }

            EnterFullscreen();
        };
        flow.Controls.Add(enterFullscreen);
        flow.Controls.Add(new Label { AutoSize = true, Text = "Escape interrupts an active capture. Evidence root: " + CaptureSettings.EvidenceRoot, MaximumSize = new Size(880, 0) });

        foreach (CheckBox item in new[] { adaptiveSync, mousePower, backgroundLoad, thermalPower, offline })
        {
            item.CheckedChanged += (_, _) => UpdateStartAvailability();
        }
    }

    private void ProcessRawMouse(long timestampTicks, NativeRawInput.MouseMessage raw)
    {
        if (detectionActive)
        {
            if (!detectedDevices.TryGetValue(raw.Device, out DeviceActivity? activity))
            {
                activity = new DeviceActivity { Path = NativeRawInput.GetDevicePath(raw.Device) };
                detectedDevices.Add(raw.Device, activity);
            }

            activity.EventCount++;
            activity.AbsoluteMovement += Math.Abs((long)raw.DeltaX) + Math.Abs((long)raw.DeltaY);
        }

        if (!capturing || evidence is null)
        {
            return;
        }

        if (raw.Device == selectedDevice)
        {
            evidence.RecordRaw(timestampTicks, selectedDeviceId, selectedDevicePath, raw);
        }
        else if (raw.DeltaX != 0 || raw.DeltaY != 0)
        {
            offTargetNonzeroEvents++;
        }
    }

    private void SelectMostActiveDevice()
    {
        detectionActive = false;
        List<KeyValuePair<IntPtr, DeviceActivity>> candidates = detectedDevices
            .Where(item => item.Value.AbsoluteMovement > 0)
            .OrderByDescending(item => item.Value.AbsoluteMovement)
            .ThenByDescending(item => item.Value.EventCount)
            .ToList();
        if (candidates.Count == 0)
        {
            devicesLabel.Text = "No moving native mouse was detected. Reset and move the SIGNO mouse again.";
            return;
        }

        KeyValuePair<IntPtr, DeviceActivity> candidate = candidates[0];
        selectedDevice = candidate.Key;
        selectedDevicePath = candidate.Value.Path;
        selectedDeviceId = NativeRawInput.StableDeviceId(selectedDevicePath);
        devicesLabel.Text = "Selected native device: " + selectedDevicePath +
            Environment.NewLine + "Stable device ID: " + selectedDeviceId +
            " • detected events: " + candidate.Value.EventCount;
        statusLabel.Text = "Native device selected. Confirm the operating conditions, then enter fullscreen.";
        UpdateStartAvailability();
    }

    private void EnterFullscreen()
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        TopMost = true;
        fullscreenReady = true;
        preflightPanel.Visible = false;
        captureLabel.Visible = true;
        captureLabel.Text = "Run " + repetitionOrdinal + " of " + CaptureSettings.PlannedRepeatCount +
            " — ready" + Environment.NewLine + Environment.NewLine + CaptureSettings.MotionInstruction +
            Environment.NewLine + Environment.NewLine + "Click anywhere to begin the 30-second native capture.";
        captureLabel.Click += BeginCaptureOnce;
    }

    private void BeginCaptureOnce(object? sender, EventArgs eventArgs)
    {
        captureLabel.Click -= BeginCaptureOnce;
        if (!ContainsFocus)
        {
            ReturnToWindowed();
            statusLabel.Text = "Capture did not start because the application was not focused.";
            return;
        }

        try
        {
            evidence = new EvidencePackage(
                repetitionOrdinal,
                evidenceState,
                selectedDeviceId,
                selectedDevicePath,
                applicationFocused: true);
            offTargetNonzeroEvents = 0;
            captureStartTicks = Stopwatch.GetTimestamp();
            previousHeartbeatTicks = captureStartTicks;
            capturing = true;
            Cursor.Hide();
        }
        catch (Exception error)
        {
            ReturnToWindowed();
            statusLabel.Text = "Capture did not start: " + error.GetType().Name + " — " + error.Message;
        }
    }

    private void OnUiTimer(object? sender, EventArgs eventArgs)
    {
        if (!capturing || evidence is null)
        {
            return;
        }

        long now = Stopwatch.GetTimestamp();
        double elapsed = (now - captureStartTicks) / (double)Stopwatch.Frequency;
        double heartbeatDelta = (now - previousHeartbeatTicks) / (double)Stopwatch.Frequency;
        previousHeartbeatTicks = now;
        evidence.RecordHeartbeat(now, heartbeatDelta, ContainsFocus);
        double remaining = Math.Max(0, CaptureSettings.TraceDurationSeconds - elapsed);
        captureLabel.Text = "CAPTURING — run " + repetitionOrdinal + " of " + CaptureSettings.PlannedRepeatCount +
            Environment.NewLine + Environment.NewLine + "Remaining: " +
            remaining.ToString("0.0", CultureInfo.InvariantCulture) + " seconds" +
            Environment.NewLine + Environment.NewLine + CaptureSettings.MotionInstruction;

        if (elapsed >= CaptureSettings.TraceDurationSeconds)
        {
            string status = offTargetNonzeroEvents == 0 ? "completed" : "interrupted";
            string reason = offTargetNonzeroEvents == 0
                ? "native-duration-completed"
                : "multiple-native-input-devices-active";
            FinalizeCapture(status, reason);
        }
    }

    private void FinalizeCapture(string status, string reason)
    {
        if (!capturing || evidence is null)
        {
            return;
        }

        capturing = false;
        Cursor.Show();
        EvidencePackage completedEvidence = evidence;
        evidence = null;
        completedEvidence.Complete(status, reason);
        string runId = completedEvidence.RunId;
        ReturnToWindowed();

        if (status == "completed")
        {
            statusLabel.Text = "Completed native run " + repetitionOrdinal + ": " + runId;
            repetitionOrdinal++;
            if (repetitionOrdinal > CaptureSettings.PlannedRepeatCount)
            {
                statusLabel.Text = "Native " + evidenceState + " capture set complete: five valid runs recorded.";
            }
        }
        else
        {
            statusLabel.Text = "Run " + repetitionOrdinal + " retained as interrupted (" + reason + "). Repeat the same ordinal.";
        }
    }

    private void ReturnToWindowed()
    {
        fullscreenReady = false;
        TopMost = false;
        WindowState = FormWindowState.Normal;
        FormBorderStyle = FormBorderStyle.Sizable;
        ClientSize = new Size(CaptureSettings.MenuWidth, CaptureSettings.MenuHeight);
        CenterToScreen();
        captureLabel.Visible = false;
        preflightPanel.Visible = true;
    }

    private bool PreflightComplete() =>
        selectedDevice != IntPtr.Zero &&
        adaptiveSync.Checked &&
        mousePower.Checked &&
        backgroundLoad.Checked &&
        thermalPower.Checked &&
        offline.Checked &&
        repetitionOrdinal <= CaptureSettings.PlannedRepeatCount;

    private void UpdateStartAvailability()
    {
        if (PreflightComplete())
        {
            statusLabel.Text = "Preflight complete. Enter native borderless fullscreen for run " + repetitionOrdinal + ".";
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (capturing)
        {
            FinalizeCapture("interrupted", "application-closing");
        }
    }
}
