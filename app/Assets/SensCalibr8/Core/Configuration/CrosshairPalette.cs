using System;
using System.Collections.Generic;

namespace SensCalibr8.Core.Configuration
{
    /// <summary>Owner-approved high-contrast profile crosshair colors. Values are persisted exactly as listed.</summary>
    public static class CrosshairPalette
    {
        private static readonly string[] colors = { "#FFE600", "#FF00FF", "#FF3B30", "#FF9500" };

        public static IReadOnlyList<string> SupportedColors => colors;

        public static bool IsSupported(string color)
        {
            foreach (string supported in colors)
                if (string.Equals(supported, color, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
