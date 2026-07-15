using System;
using UnityEditor;
using UnityEngine;

namespace SensCalibr8.Calibration.Editor
{
    [CustomEditor(typeof(CalibrationHarnessController))]
    public sealed class P0R3CalibrationHarnessEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            CalibrationHarnessController controller = (CalibrationHarnessController)target;
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode after freezing every P0-R3 plan and environment field.",
                    MessageType.Info);
                return;
            }

            if (!controller.IsCapturing)
            {
                if (GUILayout.Button("Start frozen P0-R3 capture"))
                {
                    InvokeSafely(controller.StartCapture);
                }
                return;
            }

            EditorGUILayout.LabelField("Active run", controller.CurrentRunId);
            EditorGUILayout.HelpBox(
                "Capture ends automatically at the frozen duration. Move the mouse continuously according to the recorded instruction.",
                MessageType.Warning);
            if (GUILayout.Button("Interrupt and retain evidence"))
            {
                InvokeSafely(() => controller.InterruptCapture("operator-interrupted"));
            }
        }

        private static void InvokeSafely(Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }
    }
}
