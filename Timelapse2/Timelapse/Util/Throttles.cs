using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Timelapse.Util
{
    public class Throttles
    {
        // The current setting for images rendered per second. Default is set to the maximum.
        private double desiredImageRendersPerSecond;
        public double DesiredImageRendersPerSecond
        {
            get
            {
                return this.desiredImageRendersPerSecond;
            }
            set
            {
                // ensure the value remains within the lower and upper bounds
                if (value < Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound)
                {
                    this.desiredImageRendersPerSecond = Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound;
                }
                else if (value > Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound)
                {
                    this.desiredImageRendersPerSecond = Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound;
                }
                else
                {
                    this.desiredImageRendersPerSecond = value;
                }
            }
        }

        public TimeSpan DesiredIntervalBetweenRenders
        {
            get
            {
                return TimeSpan.FromSeconds(1.0 / this.DesiredImageRendersPerSecond);
            }
        }

        public Throttles()
        {
            this.SetToSystemDefaults();
        }

        public void SetToSystemDefaults()
        {
            this.DesiredImageRendersPerSecond = Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault;
        }
    }
}
