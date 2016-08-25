using System;
using System.Windows;

namespace Timelapse.Util
{
    public class Throttles
    {
        // The current setting for images rendered per second. Default is set to the maximum.
        public double DesiredImageRendersPerSecond { get; private set; }
        public TimeSpan DesiredIntervalBetweenRenders { get; private set; }
        public int RepeatedKeyAcceptanceInterval { get; private set; }

        public Throttles()
        {
            this.ResetToDefaults();
        }

        public void ResetToDefaults()
        {
            this.DesiredImageRendersPerSecond = Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault;
        }

        public void SetDesiredImageRendersPerSecond(double rendersPerSecond)
        {
            if (rendersPerSecond < Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound ||
                rendersPerSecond > Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound)
            {
                throw new ArgumentOutOfRangeException("rendersPerSecond");
            }

            this.DesiredImageRendersPerSecond = rendersPerSecond;
            this.DesiredIntervalBetweenRenders = TimeSpan.FromSeconds(1.0 / rendersPerSecond);
            this.RepeatedKeyAcceptanceInterval = (int)(((double)SystemParameters.KeyboardSpeed + 0.5 * rendersPerSecond) / rendersPerSecond);
        }
    }
}
