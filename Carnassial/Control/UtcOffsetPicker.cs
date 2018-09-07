using System;

namespace Carnassial.Control
{
    public class UtcOffsetPicker : TimeSpanPicker
    {
        public UtcOffsetPicker()
        {
            this.Format = Constant.Time.UtcOffsetDisplayFormat;
            this.Maximum = TimeSpan.FromHours(Constant.Time.MaximumUtcOffsetInHours);
            this.Minimum = TimeSpan.FromHours(Constant.Time.MinimumUtcOffsetInHours);
        }

        protected override TimeSpan ConvertIncrementOrDecrementToTimeSpan(char partFormat, int incrementOrDecrement)
        {
            switch (partFormat)
            {
                case 'h':
                    return TimeSpan.FromHours(incrementOrDecrement);
                case 'm':
                    return TimeSpan.FromMinutes(incrementOrDecrement * Constant.Time.UtcOffsetGranularityInMinutes);
                default:
                    throw new NotSupportedException(String.Format("Unhandled part format {0}.", partFormat));
            }
        }
    }
}
