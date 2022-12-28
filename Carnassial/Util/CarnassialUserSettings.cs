using Carnassial.Database;
using System;

namespace Carnassial.Util
{
    public class CarnassialUserSettings
    {
        public LogicalOperator CustomSelectionTermCombiningOperator { get; set; }
        public MostRecentlyUsedList<string> MostRecentImageSets { get; private set; }
        public Throttles Throttles { get; private set; }

        public CarnassialUserSettings()
        {
            this.CustomSelectionTermCombiningOperator = Enum.Parse<LogicalOperator>(CarnassialSettings.Default.CustomSelectionTermCombiningOperator);
            this.MostRecentImageSets = new(CarnassialSettings.Default.MostRecentlyUsedImageSets, Constant.NumberOfMostRecentDatabasesToTrack);
            this.Throttles = new();
        }

        public void SerializeToSettings()
        {
            CarnassialSettings.Default.CustomSelectionTermCombiningOperator = this.CustomSelectionTermCombiningOperator.ToString();
            CarnassialSettings.Default.MostRecentlyUsedImageSets = this.MostRecentImageSets.ToStringCollection();
        }
    }
}
