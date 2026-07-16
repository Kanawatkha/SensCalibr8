using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Services.Calculations;
using SensCalibr8.Services.Configuration;
using SensCalibr8.Services.Validation;
using UnityEngine;

namespace SensCalibr8.Tests
{
    public sealed class P2R1CalculationValidationTests
    {
        private SensitivityCalculationService calculations;

        [SetUp]
        public void SetUp()
        {
            ResearchConstants constants = ResearchConstantsLoader.LoadFromRepository(RepositoryRoot());
            calculations = new SensitivityCalculationService(constants);
        }

        [Test]
        public void Dpi1600ProducesTheExactDocumentedPsaStartingSensitivity()
        {
            Assert.That(calculations.CalculateStartingSensitivity(1600), Is.EqualTo(0.175d));
            Assert.That(calculations.CalculateEdpi(1600, 0.175d), Is.EqualTo(280d));
        }

        [Test]
        public void PhysicalRulerFormulaUsesCountsAndCentimetersWithoutImplicitRounding()
        {
            Assert.That(calculations.CalculatePhysicalRulerDpi(1600, 2.54d), Is.EqualTo(1600d));
            double estimate = calculations.CalculatePhysicalRulerDpi(1234, 2.5d);
            Assert.That(estimate, Is.EqualTo(1234d / (2.5d / 2.54d)));
            Assert.That(estimate, Is.Not.EqualTo(Math.Round(estimate)));
        }

        [Test]
        public void EdpiBelowFloorIsAdjustedAndExplicitlyFlagged()
        {
            EdpiFloorResult adjusted = calculations.ApplyEdpiFloor(1600, 0.05d);
            Assert.That(adjusted.OriginalEdpi, Is.EqualTo(80d));
            Assert.That(adjusted.EffectiveEdpi, Is.EqualTo(160d));
            Assert.That(adjusted.EffectiveSensitivity, Is.EqualTo(0.1d));
            Assert.That(adjusted.WasAdjusted, Is.True);

            EdpiFloorResult boundary = calculations.ApplyEdpiFloor(1600, 0.1d);
            Assert.That(boundary.EffectiveEdpi, Is.EqualTo(160d));
            Assert.That(boundary.WasAdjusted, Is.False);
        }

        [Test]
        public void Cm360AndMousepadConstraintFollowTheDocumentedFormulaAndStrictBoundary()
        {
            double centimeters = calculations.CalculateCentimetersPer360(1600, 0.175d);
            Assert.That(centimeters, Is.EqualTo(1484.4155844155844d));
            Assert.That(calculations.EvaluateMousepadConstraint(1600, 0.175d, centimeters).WarningRequired, Is.False);
            Assert.That(calculations.EvaluateMousepadConstraint(1600, 0.175d, 45d).WarningRequired, Is.True);
        }

        [Test]
        public void CurrentSensitivityComparisonReturnsNormalizedEdpiRelationship()
        {
            BaselineComparisonResult equal = calculations.CompareCurrentToPsaBaseline(1600, 0.175d);
            Assert.That(equal.Relationship, Is.EqualTo(BaselineRelationship.Equal));
            Assert.That(equal.StartingSensitivity, Is.EqualTo(0.175d));
            Assert.That(equal.EdpiDifference, Is.EqualTo(0d));
            Assert.That(calculations.CompareCurrentToPsaBaseline(1600, 0.2d).Relationship, Is.EqualTo(BaselineRelationship.Above));
            Assert.That(calculations.CompareCurrentToPsaBaseline(1600, 0.1d).Relationship, Is.EqualTo(BaselineRelationship.Below));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("1600.5")]
        [TestCase("not-a-number")]
        public void HardwareDpiRejectsEveryDocumentedInvalidShape(string input)
        {
            ValidationResult<int> result = SetupInputValidationService.HardwareDpi(input);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("hardware_dpi_positive_integer_required"));
        }

        [Test]
        public void PositiveNumericSetupFieldsRejectMissingNonFiniteZeroAndNegativeValues()
        {
            foreach (Func<string, ValidationResult<double>> validator in new Func<string, ValidationResult<double>>[]
            {
                SetupInputValidationService.CurrentSensitivity,
                SetupInputValidationService.ConfiguredPollingRateHz,
                SetupInputValidationService.MousepadWidthCm,
                SetupInputValidationService.MousepadHeightCm,
                SetupInputValidationService.PhysicalRulerDistanceCm
            })
            {
                foreach (string invalid in new[] { null, string.Empty, "0", "-1", "NaN", "Infinity", "not-a-number" })
                    Assert.That(validator(invalid).IsValid, Is.False, invalid);
            }
            Assert.That(SetupInputValidationService.CurrentSensitivity("0.175").Value, Is.EqualTo(0.175d));
            Assert.That(SetupInputValidationService.ConfiguredPollingRateHz("1000").Value, Is.EqualTo(1000d));
            Assert.That(SetupInputValidationService.MousepadWidthCm("45").Value, Is.EqualTo(45d));
            Assert.That(SetupInputValidationService.MousepadHeightCm("40").Value, Is.EqualTo(40d));
        }

        [Test]
        public void PhysicalRulerCountsRequireAPositiveInteger()
        {
            foreach (string invalid in new[] { null, string.Empty, "0", "-1", "1.5", "not-a-number" })
                Assert.That(SetupInputValidationService.PhysicalRulerCounts(invalid).IsValid, Is.False, invalid);
            Assert.That(SetupInputValidationService.PhysicalRulerCounts("1600").Value, Is.EqualTo(1600L));
        }

        [Test]
        public void CalculationTypesArePlainImmutableCSharpContracts()
        {
            Assert.That(typeof(SensitivityCalculationService).BaseType, Is.EqualTo(typeof(object)));
            AssertNoPublicSetters(typeof(EdpiFloorResult));
            AssertNoPublicSetters(typeof(BaselineComparisonResult));
            AssertNoPublicSetters(typeof(MousepadConstraintResult));
            AssertNoPublicSetters(typeof(ValidationResult<double>));
        }

        [Test]
        public void TypedCalculationBoundaryAlsoRejectsInvalidValues()
        {
            Assert.That(() => calculations.CalculateStartingSensitivity(0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => calculations.CalculateEdpi(-1, 0.175d), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => calculations.CalculateEdpi(1600, 0d), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => calculations.CalculateEdpi(1600, double.NaN), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => calculations.CalculatePhysicalRulerDpi(0, 2.54d), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => calculations.CalculatePhysicalRulerDpi(1600, 0d), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => calculations.EvaluateMousepadConstraint(1600, 0.175d, 0d), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static void AssertNoPublicSetters(Type type)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                Assert.That(property.SetMethod, Is.Null, type.Name + "." + property.Name);
        }

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
