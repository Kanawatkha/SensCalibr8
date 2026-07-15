using System;

namespace SensCalibr8.Calibration
{
    public static class P0R4GeometryMath
    {
        public static double HorizontalToVerticalFovDegrees(
            double horizontalFovDegrees,
            double aspectRatio)
        {
            RequirePositive(horizontalFovDegrees, nameof(horizontalFovDegrees));
            RequirePositive(aspectRatio, nameof(aspectRatio));
            if (horizontalFovDegrees >= 180.0)
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalFovDegrees));
            }

            double horizontalRadians = horizontalFovDegrees * Math.PI / 180.0;
            return 2.0 * Math.Atan(Math.Tan(horizontalRadians / 2.0) / aspectRatio)
                * 180.0 / Math.PI;
        }

        public static double AngularDiameterToWorld(
            double angularDiameterDegrees,
            double distanceWorld)
        {
            RequirePositive(angularDiameterDegrees, nameof(angularDiameterDegrees));
            RequirePositive(distanceWorld, nameof(distanceWorld));
            return 2.0 * distanceWorld * Math.Tan(
                angularDiameterDegrees * Math.PI / 360.0);
        }

        public static double FittsIndexOfDifficulty(
            double distanceDegrees,
            double targetWidthDegrees)
        {
            RequirePositive(distanceDegrees, nameof(distanceDegrees));
            RequirePositive(targetWidthDegrees, nameof(targetWidthDegrees));
            return Math.Log(2.0 * distanceDegrees / targetWidthDegrees, 2.0);
        }

        private static void RequirePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
