using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SensCalibr8.Calibration.Tests
{
    public sealed class P0R4GeometryParityTests
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        [Test]
        public void HorizontalFovConversionMatchesUnityAndPythonEvidence()
        {
            GeometryEvidence evidence = LoadEvidence();
            double plainResult = P0R4GeometryMath.HorizontalToVerticalFovDegrees(
                evidence.derived.horizontal_fov_deg,
                evidence.derived.aspect_ratio);
            float unityResult = Camera.HorizontalToVerticalFieldOfView(
                (float)evidence.derived.horizontal_fov_deg,
                (float)evidence.derived.aspect_ratio);

            Assert.That(plainResult, Is.EqualTo(evidence.derived.vertical_fov_deg).Within(1e-10));
            Assert.That(unityResult, Is.EqualTo(evidence.derived.vertical_fov_deg).Within(1e-4));
        }

        [Test]
        public void UnityProjectionMatchesAllThreeDerivedTargetDiameters()
        {
            GeometryEvidence evidence = LoadEvidence();
            GameObject cameraObject = new GameObject("P0-R4 parity camera");
            try
            {
                Camera camera = ConfigureCamera(cameraObject, evidence);
                AssertProjectedDiameter(camera, evidence.derived.target_geometry.small, evidence);
                AssertProjectedDiameter(camera, evidence.derived.target_geometry.medium, evidence);
                AssertProjectedDiameter(camera, evidence.derived.target_geometry.large, evidence);
            }
            finally
            {
                DestroyCameraObject(cameraObject);
            }
        }

        [Test]
        public void UnityProjectionMatchesHorizontalAndVerticalSpawnExtremes()
        {
            GeometryEvidence evidence = LoadEvidence();
            GameObject cameraObject = new GameObject("P0-R4 parity camera");
            try
            {
                Camera camera = ConfigureCamera(cameraObject, evidence);
                float distance = (float)evidence.derived.target_plane_distance_world;
                float horizontalWorld = distance * Mathf.Tan(40f * Mathf.Deg2Rad);
                float verticalWorld = distance * Mathf.Tan(25f * Mathf.Deg2Rad);
                Vector3 horizontalScreen = camera.WorldToScreenPoint(
                    new Vector3(horizontalWorld, 0f, distance));
                Vector3 verticalScreen = camera.WorldToScreenPoint(
                    new Vector3(0f, verticalWorld, distance));

                Assert.That(
                    horizontalScreen.x - ReferenceWidth / 2f,
                    Is.EqualTo(evidence.derived.safe_viewport.maximum_condition_horizontal_offset_px)
                        .Within(1e-3));
                Assert.That(
                    verticalScreen.y - ReferenceHeight / 2f,
                    Is.EqualTo(evidence.derived.safe_viewport.configured_vertical_offset_px)
                        .Within(1e-3));
            }
            finally
            {
                DestroyCameraObject(cameraObject);
            }
        }

        [Test]
        public void PlainFittsCalculationPreservesDistanceAndWidthMonotonicity()
        {
            double closeSmall = P0R4GeometryMath.FittsIndexOfDifficulty(5.0, 0.75);
            double farSmall = P0R4GeometryMath.FittsIndexOfDifficulty(40.0, 0.75);
            double farLarge = P0R4GeometryMath.FittsIndexOfDifficulty(40.0, 2.25);

            Assert.That(farSmall, Is.GreaterThan(closeSmall));
            Assert.That(farSmall, Is.GreaterThan(farLarge));
        }

        [Test]
        public void PlainGeometryRejectsInvalidInputs()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => P0R4GeometryMath.HorizontalToVerticalFovDegrees(180.0, 16.0 / 9.0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => P0R4GeometryMath.AngularDiameterToWorld(0.0, 10.0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => P0R4GeometryMath.FittsIndexOfDifficulty(5.0, -1.0));
        }

        private static Camera ConfigureCamera(GameObject cameraObject, GeometryEvidence evidence)
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = (float)evidence.derived.vertical_fov_deg;
            camera.aspect = (float)evidence.derived.aspect_ratio;
            camera.targetTexture = new RenderTexture(
                (int)ReferenceWidth,
                (int)ReferenceHeight,
                24);
            return camera;
        }

        private static void DestroyCameraObject(GameObject cameraObject)
        {
            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera != null && camera.targetTexture != null)
            {
                RenderTexture targetTexture = camera.targetTexture;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(targetTexture);
            }
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }

        private static void AssertProjectedDiameter(
            Camera camera,
            TargetGeometry target,
            GeometryEvidence evidence)
        {
            float distance = (float)evidence.derived.target_plane_distance_world;
            float radius = (float)target.world_diameter / 2f;
            Vector3 left = camera.WorldToScreenPoint(new Vector3(-radius, 0f, distance));
            Vector3 right = camera.WorldToScreenPoint(new Vector3(radius, 0f, distance));
            Assert.That(
                right.x - left.x,
                Is.EqualTo(target.projected_pixel_diameter).Within(1e-3));
        }

        private static GeometryEvidence LoadEvidence()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "evidence",
                "p0-r4",
                "p0-r4-geometry-derived-v1.json"));
            Assert.That(File.Exists(path), Is.True, path);
            GeometryEvidence evidence = JsonUtility.FromJson<GeometryEvidence>(File.ReadAllText(path));
            Assert.That(evidence, Is.Not.Null);
            Assert.That(evidence.accepted, Is.True);
            return evidence;
        }

        [Serializable]
        private sealed class GeometryEvidence
        {
            public bool accepted;
            public DerivedGeometry derived;
        }

        [Serializable]
        private sealed class DerivedGeometry
        {
            public double aspect_ratio;
            public double horizontal_fov_deg;
            public double vertical_fov_deg;
            public double target_plane_distance_world;
            public TargetGeometrySet target_geometry;
            public SafeViewport safe_viewport;
        }

        [Serializable]
        private sealed class TargetGeometrySet
        {
            public TargetGeometry small;
            public TargetGeometry medium;
            public TargetGeometry large;
        }

        [Serializable]
        private sealed class TargetGeometry
        {
            public double world_diameter;
            public double projected_pixel_diameter;
        }

        [Serializable]
        private sealed class SafeViewport
        {
            public double maximum_condition_horizontal_offset_px;
            public double configured_vertical_offset_px;
        }
    }
}
