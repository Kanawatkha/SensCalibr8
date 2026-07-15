using System.Diagnostics;

namespace SensCalibr8.Calibration
{
    public sealed class CalibrationClock
    {
        public long Frequency
        {
            get { return Stopwatch.Frequency; }
        }

        public CalibrationTimestamp Capture()
        {
            long ticks = Stopwatch.GetTimestamp();
            return new CalibrationTimestamp
            {
                Ticks = ticks,
                Seconds = (double)ticks / Stopwatch.Frequency
            };
        }
    }
}
