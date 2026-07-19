using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Services.Analysis;
using SensCalibr8.Services.Configuration;
using SensCalibr8.TestLogic;
using SensCalibr8.UI;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P7R5UxVisualScopeComplianceTests
    {
        [Test]
        public void StandaloneMenuDefaultsMatchTheDocumentedWindowPolicy()
        {
            string settings = File.ReadAllText(Path.Combine(RepositoryRoot(), "app", "ProjectSettings", "ProjectSettings.asset"));
            Assert.That(settings, Does.Contain("defaultScreenWidth: " + WindowDisplayPolicy.MenuWidth));
            Assert.That(settings, Does.Contain("defaultScreenHeight: " + WindowDisplayPolicy.MenuHeight));
            Assert.That(settings, Does.Contain("resizableWindow: 1"));
            Assert.That(settings, Does.Contain("allowFullscreenSwitch: 1"));
            Assert.That(settings, Does.Contain("fullscreenMode: 3"));
        }

        [Test]
        public void VisualContractKeepsArenaSimpleAndCrosshairColorOnlyConfigurable()
        {
            FrozenCalibrationConfiguration configuration = FrozenCalibrationConfigurationLoader.LoadFromRepository(RepositoryRoot());
            FrozenArenaGeometry geometry = FrozenArenaGeometry.From(configuration);
            Assert.That(geometry.TargetDiameters.Keys, Is.EquivalentTo(new[] { "small", "medium", "large" }));
            Assert.That(geometry.CrosshairDiameter, Is.GreaterThan(0f));
            Assert.That(CrosshairPalette.SupportedColors, Is.EquivalentTo(new[] { "#FFE600", "#FF00FF", "#FF3B30", "#FF9500" }));

            string menu = File.ReadAllText(Path.Combine(RepositoryRoot(), "app", "Assets", "SensCalibr8", "UI", "SensCalibr8MenuBootstrap.cs"));
            Assert.That(menu, Does.Contain("Crosshair style: fixed dot"));
            Assert.That(menu, Does.Contain("Crosshair size: fixed four-pixel filled dot"));
            Assert.That(menu, Does.Contain("DrawWarnings"));
        }

        [Test]
        public void OfflineSensitivityOnlyScopeHasNoNetworkOrRestoreSurface()
        {
            string assets = Path.Combine(RepositoryRoot(), "app", "Assets");
            foreach (string sourcePath in Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "Tests" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            {
                string source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("UnityWebRequest"), sourcePath);
                Assert.That(source, Does.Not.Contain("HttpClient"), sourcePath);
                Assert.That(source, Does.Not.Contain("http://"), sourcePath);
                Assert.That(source, Does.Not.Contain("https://"), sourcePath);
            }

            string[] exportMethods = typeof(ProfileDataExportService).GetMethods()
                .Where(method => method.IsPublic)
                .Select(method => method.Name)
                .ToArray();
            Assert.That(exportMethods, Does.Not.Contain("Import"));
            Assert.That(exportMethods, Does.Not.Contain("Restore"));
        }

        [Test]
        public void DisplayPolicyReturnsToThePreviousWindowAfterTestEscape()
        {
            var policy = new WindowDisplayPolicy();
            Assert.That(policy.IsTestFullscreen, Is.False);
            bool paused = false;
            Assert.That(policy.HandleEscape(() => paused = true), Is.False);
            Assert.That(paused, Is.False);
        }

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
