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

        protected override TimeSpan ConvertIncrementOrDecrementToTimeSpan(char partFormat, int incrementOrDecrement)
        {
            switch (partFormat)
            {
                case 'h':
                    return TimeSpan.FromHours(incrementOrDecrement);
                case 'm':
                    return TimeSpan.FromTicks(incrementOrDecrement * Constant.Time.UtcOffsetGranularity.Ticks);
                default:
                    throw new NotSupportedException(String.Format("Unhandled part format {0}.", partFormat));
            }
        }
    }
}
