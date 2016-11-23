using System;

namespace Carnassial.Controls
{
    public class UtcOffsetPicker : TimeSpanPicker
    {
        public UtcOffsetPicker()
        {
            this.Format = Constant.Time.UtcOffsetDisplayFormat;
            this.Maximum = Constant.Time.MaximumUtcOffset;
            this.Minimum = Constant.Time.MinimumUtcOffset;
        }

        protected override TimeSpan ConvertIncrementToTimeSpan(string partFormat, int increment)
        {
            switch (partFormat)
            {
                case "h":
                    return TimeSpan.FromHours(increment);
                case "m":
                    return TimeSpan.FromTicks(increment * Constant.Time.UtcOffsetGranularity.Ticks);
                default:
                    throw new NotSupportedException(String.Format("Unhandled part format {0}.", partFormat));
            }
        }
    }
}
