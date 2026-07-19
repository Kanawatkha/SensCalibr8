using System;
using System.Collections.Generic;
using System.IO;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using SensCalibr8.Services.Profiles;
using SensCalibr8.Services.Analysis;
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
        private bool showingComparison;
        private readonly Dictionary<long, bool> comparisonSelection = new Dictionary<long, bool>();
        private IReadOnlyList<ProfileComparisonPresentation> comparisonRows = Array.Empty<ProfileComparisonPresentation>();

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
            else if (showingSetup) DrawSetup(); else if (showingDashboard) DrawDashboard(); else if (showingComparison) DrawComparison(); else DrawSlots();
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
            if (GUILayout.Button("Open Comparison Page")) { comparisonSelection.Clear(); comparisonRows = Array.Empty<ProfileComparisonPresentation>(); showingComparison = true; }
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
            ImmediateFeedbackPresentation feedback = dashboard.ImmediateFeedback;
            GUILayout.Label("Best Sensitivity: " + (feedback.BestSensitivity.HasValue ? feedback.BestSensitivity.Value.ToString("G17") : "not available until Phase 3 has a unique Winner."));
            GUILayout.Label("Current Grade: " + (string.IsNullOrWhiteSpace(dashboard.LatestGrade) ? "not available until scoring is complete." : dashboard.LatestGrade));
            GUILayout.Label("Latest Performance Score: " + (feedback.LatestPerformanceScore.HasValue ? feedback.LatestPerformanceScore.Value.ToString("G17") : "not available until a complete scored battery exists."));
            GUILayout.Label("Recent activity: " + dashboard.CompletedSessionCount + " completed session(s)" + (string.IsNullOrWhiteSpace(dashboard.LatestSessionDate) ? "." : ", latest " + dashboard.LatestSessionDate));
            DrawAccuracyChart(feedback);
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
            if (GUILayout.Button("Open Comparison Page")) { comparisonSelection.Clear(); comparisonRows = Array.Empty<ProfileComparisonPresentation>(); showingDashboard = false; showingComparison = true; }
        }

        private void DrawComparison()
        {
            GUILayout.Label("Profile Comparison");
            GUILayout.Label("Select at least two profiles explicitly. Only selected profiles are read; raw in-game sensitivity is never compared.");
            foreach (ProfileSlotPresentation slot in slots)
            {
                bool selected = comparisonSelection.TryGetValue(slot.Id, out bool current) && current;
                comparisonSelection[slot.Id] = GUILayout.Toggle(selected, slot.Name);
            }
            if (GUILayout.Button("Compare Selected Profiles"))
            {
                var ids = new List<long>();
                foreach (KeyValuePair<long, bool> entry in comparisonSelection) if (entry.Value) ids.Add(entry.Key);
                try { comparisonRows = application.CompareExplicitProfiles(ids); slotMessage = string.Empty; }
                catch (Exception exception) { comparisonRows = Array.Empty<ProfileComparisonPresentation>(); slotMessage = exception.Message; }
            }
            if (comparisonRows.Count > 0)
            {
                GUILayout.Label("Profile | eDPI | Consistency utility | Reaction Time Tier | Performance Score");
                foreach (ProfileComparisonPresentation row in comparisonRows)
                {
                    string metrics = row.HasComparableResult
                        ? row.ProfileName + " | " + row.Edpi.Value.ToString("G17") + " | " + row.ConsistencyUtility.Value.ToString("G17") + " | " + row.ReactionTier + " | " + row.PerformanceScore.Value.ToString("G17")
                        : row.ProfileName + " | no complete scored/graded battery available.";
                    GUILayout.Label(metrics);
                }
                GUILayout.Label("Values are persisted outputs normalized through eDPI. This view is descriptive only and does not rank skill or infer causation.");
            }
            if (GUILayout.Button("Back to Slots")) { showingComparison = false; comparisonRows = Array.Empty<ProfileComparisonPresentation>(); }
            if (!string.IsNullOrWhiteSpace(slotMessage)) GUILayout.Label(slotMessage);
        }

        private static void DrawAccuracyChart(ImmediateFeedbackPresentation feedback)
        {
            GUILayout.Label("Current Protocol Accuracy by Sensitivity");
            if (feedback.AccuracyBars.Count == 0)
            {
                GUILayout.Label("No finalized post-adaptation shot evidence is available for the current protocol window.");
                return;
            }
            GUILayout.Label("Mode: " + feedback.CurrentMode + " | Cycle: " + feedback.CurrentCycleId + " | Phase: " + feedback.CurrentPhase);
            foreach (AccuracyBarPresentation bar in feedback.AccuracyBars)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Sensitivity " + bar.SensitivityValue.ToString("G17") + " | " + bar.AccuracyPercent.ToString("G17") + "% (" + bar.HitCount + "/" + bar.ResolvedCount + ")", GUILayout.Width(300f));
                GUI.enabled = false;
                GUILayout.HorizontalSlider((float)bar.FillFraction, 0f, 1f, GUILayout.Width(220f));
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
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
