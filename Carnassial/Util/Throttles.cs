using System;
using System.Windows;
using System.Windows.Threading;

namespace Carnassial.Util
{
    public class Throttles
    {
        public double DesiredImageRendersPerSecond { get; private set; }
        public TimeSpan DesiredIntervalBetweenRenders { get; private set; }
        public DispatcherTimer FilePlayTimer { get; private set; }
        public int RepeatedKeyAcceptanceInterval { get; private set; }

        public Throttles()
        {
            this.FilePlayTimer = new DispatcherTimer();
            this.ResetToDefaults();
        }

        public TimeSpan GetDesiredProgressUpdateInterval()
        {
            if (this.DesiredIntervalBetweenRenders > Constant.ThrottleValues.DesiredIntervalBetweenStatusUpdates)
            {
                return this.DesiredIntervalBetweenRenders;
            }
            return Constant.ThrottleValues.DesiredIntervalBetweenStatusUpdates;
        }

        public void ResetToDefaults()
        {
            this.SetDesiredImageRendersPerSecond(Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault);
        }

        public void SetDesiredImageRendersPerSecond(double rendersPerSecond)
        {
            if (rendersPerSecond < Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound ||
                rendersPerSecond > Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound)
            {
                throw new ArgumentOutOfRangeException(nameof(rendersPerSecond));
            }

            this.DesiredImageRendersPerSecond = rendersPerSecond;
            this.DesiredIntervalBetweenRenders = TimeSpan.FromSeconds(1.0 / rendersPerSecond);
            this.RepeatedKeyAcceptanceInterval = (int)(((double)SystemParameters.KeyboardSpeed + 0.5 * rendersPerSecond) / rendersPerSecond);
            this.FilePlayTimer.Interval = this.DesiredIntervalBetweenRenders;
        }
    }
}
