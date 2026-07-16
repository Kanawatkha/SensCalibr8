using System;
using System.Collections.Generic;
using System.IO;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Services.Profiles;
using UnityEngine;

namespace SensCalibr8.UI
{
    public sealed class SensCalibr8MenuBootstrap : MonoBehaviour
    {
        private string[] supportedCrosshairColors = Array.Empty<string>();

        private readonly ProfileSetupScreenModel setup = new ProfileSetupScreenModel();
        private ProfileSetupApplicationService application;
        private IReadOnlyList<ProfileSlotPresentation> slots = Array.Empty<ProfileSlotPresentation>();
        private string selectedCrosshairColor;
        private string startupError;
        private string slotMessage;
        private string editingCrosshairColor;
        private long? editingProfileId;
        private ProfileDeletionConfirmation pendingDeletion;
        private IReadOnlyList<ErgonomicWarningPresentation> warnings = Array.Empty<ErgonomicWarningPresentation>();
        private ProfileDashboardPresentation dashboard;
        private bool showingSetup;
        private bool showingDashboard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateMenu()
        {
            if (FindFirstObjectByType<SensCalibr8MenuBootstrap>() == null)
                new GameObject("SensCalibr8 Menu UI").AddComponent<SensCalibr8MenuBootstrap>();
        }

        private void Awake()
        {
            try
            {
                supportedCrosshairColors = CopySupportedCrosshairColors();
                string repositoryRoot = FindRepositoryRoot(Application.dataPath);
                string databasePath = Path.Combine(Application.persistentDataPath, "senscalibr8.sqlite3");
                string nativeLibraryPath = Path.Combine(Application.dataPath, "Plugins", "sqlite3.dll");
                application = ProfileSetupApplicationFactory.Open(databasePath, repositoryRoot, nativeLibraryPath);
                slots = application.ListSlots();
            }
            catch (Exception exception) { startupError = exception.Message; }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20f, 20f, Screen.width - 40f, Screen.height - 40f));
            GUILayout.Label("SensCalibr8");
            if (!string.IsNullOrWhiteSpace(startupError)) GUILayout.Label("Startup error: " + startupError);
            else if (showingSetup) DrawSetup(); else if (showingDashboard) DrawDashboard(); else DrawSlots();
            GUILayout.EndArea();
        }

        private void DrawSlots()
        {
            GUILayout.Label("Profile Slots");
            ProfileSetupPresentation active = application.GetActiveSetup();
            if (active != null)
            {
                GUILayout.Label("Active profile: " + active.Name);
                if (GUILayout.Button("Exit Active Profile")) { application.ExitActiveProfile(); slotMessage = "Active profile exited."; }
            }
            foreach (ProfileSlotPresentation slot in slots)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(slot.Name + " | Last active: " + slot.LastActiveDate);
                if (GUILayout.Button("Select"))
                {
                    application.SelectSlot(slot.Id);
                    slots = application.ListSlots();
                    slotMessage = "Profile selected.";
                    OpenDashboard();
                }
                if (GUILayout.Button("Edit"))
                {
                    ProfileSetupPresentation profile = application.GetSetup(slot.Id);
                    setup.Load(profile);
                    editingProfileId = slot.Id;
                    editingCrosshairColor = profile.CrosshairColor;
                    showingSetup = true;
                }
                if (GUILayout.Button("Delete")) BeginDeletion(slot.Id);
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Create New Slot")) showingSetup = true;
            DrawDeletionConfirmation();
            if (!string.IsNullOrWhiteSpace(slotMessage)) GUILayout.Label(slotMessage);
        }

        private void DrawSetup()
        {
            GUILayout.Label(editingProfileId.HasValue ? "Edit Setup" : "Setup");
            InputRow("Profile name", ref setup.Name);
            setup.UsePhysicalRuler = GUILayout.Toggle(setup.UsePhysicalRuler, "Use Physical Ruler Test");
            if (setup.UsePhysicalRuler) DrawPhysicalRuler(); else InputRow("Hardware DPI", ref setup.HardwareDpi);
            InputRow("Current in-game sensitivity", ref setup.CurrentSensitivity);
            InputRow("Configured polling rate (Hz)", ref setup.ConfiguredPollingRateHz);
            setup.DominantHand = (DominantHand)GUILayout.Toolbar((int)setup.DominantHand, Enum.GetNames(typeof(DominantHand)));
            if (editingProfileId.HasValue) GUILayout.Label("Crosshair color (locked): " + editingCrosshairColor); else DrawCrosshair();
            GUILayout.Label("Crosshair style: fixed dot");
            GUILayout.Label("Crosshair size: fixed four-pixel filled dot");
            setup.GripStyle = (GripStyle)GUILayout.Toolbar((int)setup.GripStyle, Enum.GetNames(typeof(GripStyle)));
            setup.MovementStrategy = (MovementStrategy)GUILayout.Toolbar((int)setup.MovementStrategy, Enum.GetNames(typeof(MovementStrategy)));
            InputRow("Mousepad width (cm)", ref setup.MousepadWidthCm);
            InputRow("Mousepad height (cm)", ref setup.MousepadHeightCm);
            InputRow("ADS multiplier (reference only)", ref setup.AdsMultiplier);
            if (!editingProfileId.HasValue && GUILayout.Button("Create Profile") && application != null && setup.TryCreate(application, selectedCrosshairColor, out ProfileSlotPresentation created))
            {
                slots = application.ListSlots();
                showingSetup = false;
                application.SelectSlot(created.Id);
                OpenDashboard();
            }
            if (editingProfileId.HasValue && GUILayout.Button("Save Setup") && application != null && setup.TryUpdate(application, editingProfileId.Value, out _))
            {
                slots = application.ListSlots();
                showingSetup = false;
                editingProfileId = null;
                editingCrosshairColor = null;
                if (application.GetActiveSetup() != null) OpenDashboard();
            }
            if (GUILayout.Button("Back to Slots")) { showingSetup = false; editingProfileId = null; editingCrosshairColor = null; }
            if (!string.IsNullOrWhiteSpace(setup.StatusMessage)) GUILayout.Label(setup.StatusMessage);
        }

        private void DrawPhysicalRuler()
        {
            InputRow("Detected counts", ref setup.PhysicalRulerCounts);
            InputRow("Measured distance (cm)", ref setup.PhysicalRulerDistanceCm);
            if (GUILayout.Button("Calculate Physical Ruler DPI") && application != null) setup.TryPreviewPhysicalRuler(application);
            if (setup.PhysicalRulerPreview != null)
            {
                GUILayout.Label("Exact DPI estimate: " + setup.PhysicalRulerPreview.ExactEstimatedDpi.ToString("G17"));
                InputRow("Confirmed DPI", ref setup.ConfirmedPhysicalRulerDpi);
                setup.IsPhysicalRulerDpiConfirmed = GUILayout.Toggle(setup.IsPhysicalRulerDpiConfirmed, "I confirm the DPI to store");
            }
        }

        private void DrawDashboard()
        {
            if (dashboard == null) { showingDashboard = false; return; }
            ProfileSetupPresentation active = dashboard.Profile;
            GUILayout.Label("Dashboard | " + active.Name);
            GUILayout.Label("Best Sensitivity: not available until the calibration protocol is complete.");
            GUILayout.Label("Current Grade: " + (string.IsNullOrWhiteSpace(dashboard.LatestGrade) ? "not available until scoring is complete." : dashboard.LatestGrade));
            GUILayout.Label("Recent activity: " + dashboard.CompletedSessionCount + " completed session(s)" + (string.IsNullOrWhiteSpace(dashboard.LatestSessionDate) ? "." : ", latest " + dashboard.LatestSessionDate));
            DrawWarnings();
            GUILayout.Label("Test Modes");
            GUI.enabled = false;
            GUILayout.Button("Flick — Close Range (available after Phase 3/4)");
            GUILayout.Button("Flick — Far Range (available after Phase 3/4)");
            GUILayout.Button("Tracking (available after Phase 3/4)");
            GUILayout.Button("Micro-Correction (available after Phase 3/4)");
            GUI.enabled = true;
            if (GUILayout.Button("Edit Setup"))
            {
                setup.Load(active);
                editingProfileId = active.Id;
                editingCrosshairColor = active.CrosshairColor;
                showingDashboard = false;
                showingSetup = true;
            }
            if (GUILayout.Button("Back to Slots")) showingDashboard = false;
        }

        private void DrawWarnings()
        {
            foreach (ErgonomicWarningPresentation warning in warnings)
            {
                if (warning.Acknowledged) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(warning.Message);
                if (GUILayout.Button("Acknowledge"))
                {
                    try { application.AcknowledgeActiveWarning(warning.Id); dashboard = application.GetActiveDashboard(); warnings = dashboard.Warnings; }
                    catch (ProfileLifecycleException exception) { slotMessage = exception.ErrorCode; }
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawCrosshair()
        {
            GUILayout.Label("Crosshair color (locked after creation)");
            if (supportedCrosshairColors == null || supportedCrosshairColors.Length == 0)
            {
                GUILayout.Label("No approved color palette is configured.");
                selectedCrosshairColor = string.Empty;
                return;
            }
            int index = Array.IndexOf(supportedCrosshairColors, selectedCrosshairColor);
            index = GUILayout.Toolbar(Math.Max(0, index), supportedCrosshairColors);
            selectedCrosshairColor = supportedCrosshairColors[index];
        }

        private static void InputRow(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label);
            value = GUILayout.TextField(value ?? string.Empty);
            GUILayout.EndHorizontal();
        }

        private void BeginDeletion(long profileId)
        {
            try
            {
                pendingDeletion = application.BeginDeletion(profileId);
                slotMessage = "Deletion requires confirmation.";
            }
            catch (ProfileLifecycleException exception) { slotMessage = exception.ErrorCode; }
        }

        private void OpenDashboard()
        {
            try
            {
                dashboard = application.GetActiveDashboard();
                warnings = dashboard == null ? Array.Empty<ErgonomicWarningPresentation>() : dashboard.Warnings;
                showingDashboard = true;
            }
            catch (Exception exception) { slotMessage = exception.Message; }
        }

        private void DrawDeletionConfirmation()
        {
            if (pendingDeletion == null) return;
            GUILayout.Label("Delete '" + pendingDeletion.ProfileName + "'? This permanently removes its history.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Confirm Delete"))
            {
                try
                {
                    application.ConfirmDeletion(pendingDeletion);
                    pendingDeletion = null;
                    slots = application.ListSlots();
                    slotMessage = "Profile deleted.";
                }
                catch (ProfileLifecycleException exception) { pendingDeletion = null; slotMessage = exception.ErrorCode; }
            }
            if (GUILayout.Button("Cancel Delete")) { pendingDeletion = null; slotMessage = "Deletion cancelled."; }
            GUILayout.EndHorizontal();
        }

        private static string[] CopySupportedCrosshairColors()
        {
            var result = new string[CrosshairPalette.SupportedColors.Count];
            for (int index = 0; index < result.Length; index++) result[index] = CrosshairPalette.SupportedColors[index];
            return result;
        }

        private static string FindRepositoryRoot(string startingDirectory)
        {
            var current = new DirectoryInfo(startingDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "calibration"))) return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException("SensCalibr8 calibration configuration could not be located.");
        }
    }
}
