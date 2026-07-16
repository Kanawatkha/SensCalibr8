using System;
using System.IO;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Services.Configuration;
using SensCalibr8.TestLogic;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P3R3CalibratedArenaRuntimeTests
    {
        private FrozenCalibrationConfiguration configuration;

        [SetUp]
        public void SetUp() => configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());

        [Test]
        public void AcceptedGeometryMapsTheFrozenCameraArenaAndTargetContracts()
        {
            FrozenArenaGeometry geometry = FrozenArenaGeometry.From(configuration);
            Assert.That(geometry.CameraPosition, Is.EqualTo(new Vector3(0f, 1.6f, 0f)));
            Assert.That(geometry.VerticalFov, Is.EqualTo(70.53280043291679f).Within(0.0001f));
            Assert.That(geometry.ReferenceAspect, Is.EqualTo(16f / 9f).Within(0.0001f));
            Assert.That(geometry.ArenaDimensions, Is.EqualTo(new Vector3(20f, 12f, 21f)));
            Assert.That(geometry.TargetColor, Is.EqualTo(Color.cyan));
            Assert.That(geometry.TargetDiameters["small"], Is.EqualTo(0.13090156304068037f).Within(0.000001f));
            Assert.That(geometry.CrosshairDiameter, Is.EqualTo(4f));
            Assert.That(geometry.HudReserve, Is.EqualTo(64f));
        }

        [Test]
        public void LetterboxPreservesTheFrozenAspectOnBothDisplayShapes()
        {
            Rect wide = LetterboxedViewport.Calculate(2560f, 1080f, 16f / 9f);
            Rect tall = LetterboxedViewport.Calculate(1080f, 1920f, 16f / 9f);
            Assert.That(wide.width, Is.EqualTo(0.75f).Within(0.0001f)); Assert.That(wide.x, Is.EqualTo(0.125f).Within(0.0001f));
            Assert.That(tall.height, Is.EqualTo(0.31640625f).Within(0.0001f)); Assert.That(tall.y, Is.EqualTo(0.341796875f).Within(0.0001f));
        }

        [Test]
        public void TargetServiceAcceptsOnlyFrozenSizesAndBuildsAnUnlitCyanSphere()
        {
            GameObject serviceObject = new GameObject("test target service");
            try
            {
                ArenaTargetService service = serviceObject.AddComponent<ArenaTargetService>();
                service.Configure(FrozenArenaGeometry.From(configuration));
                service.Show("medium", new Vector3(1f, 2f, 3f));
                GameObject target = GameObject.Find("Cyan Calibration Target");
                Assert.That(target, Is.Not.Null); Assert.That(target.transform.localScale.x, Is.EqualTo(0.26181434169670176f).Within(0.000001f));
                Assert.That(target.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                Assert.That(() => service.Show("unknown", Vector3.zero), Throws.TypeOf<ArgumentException>());
                UnityEngine.Object.DestroyImmediate(target);
            }
            finally { UnityEngine.Object.DestroyImmediate(serviceObject); }
        }

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
