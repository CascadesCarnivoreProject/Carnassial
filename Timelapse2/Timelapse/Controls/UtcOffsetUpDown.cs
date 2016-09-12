﻿using System;
using System.Reflection;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Controls
{
    public class UtcOffsetUpDown : TimeSpanUpDown
    {
        private static readonly FieldInfo DateTimeInfoListInfo;
        private static readonly PropertyInfo DateTimeInfoListCount;
        private static readonly MethodInfo DateTimeInfoListRemoveRange;

        static UtcOffsetUpDown()
        {
            UtcOffsetUpDown.DateTimeInfoListInfo = typeof(TimeSpanUpDown).GetField("_dateTimeInfoList", BindingFlags.Instance | BindingFlags.NonPublic);
            Type typeofListDateTimeInfo = UtcOffsetUpDown.DateTimeInfoListInfo.FieldType;
            UtcOffsetUpDown.DateTimeInfoListCount = typeofListDateTimeInfo.GetProperty("Count");
            UtcOffsetUpDown.DateTimeInfoListRemoveRange = typeofListDateTimeInfo.GetMethod("RemoveRange");
        }

        public UtcOffsetUpDown()
        {
            this.Maximum = Constants.Time.MaximumUtcOffset;
            this.Minimum = Constants.Time.MinimumUtcOffset;
        }

        protected override void OnCurrentDateTimePartChanged(DateTimePart oldValue, DateTimePart newValue)
        {
            base.OnCurrentDateTimePartChanged(oldValue, newValue);

            switch (newValue)
            {
                case DateTimePart.Hour12:
                case DateTimePart.Hour24:
                    this.Step = 1;
                    break;
                case DateTimePart.Minute:
                    this.Step = Constants.Time.UtcOffsetGranularity.Minutes;
                    break;
                default:
                    this.Step = 0;
                    break;
            }
        }

        protected override void InitializeDateTimeInfoList()
        {
            base.InitializeDateTimeInfoList();

            object dateTimeInfoList = UtcOffsetUpDown.DateTimeInfoListInfo.GetValue(this);
            int dateTimeInfoListCount = (int)UtcOffsetUpDown.DateTimeInfoListCount.GetValue(dateTimeInfoList, null);
            int desiredCount = dateTimeInfoListCount > 5 ? 4 : 3;
            UtcOffsetUpDown.DateTimeInfoListRemoveRange.Invoke(dateTimeInfoList, new object[] { desiredCount, dateTimeInfoListCount - desiredCount });
        }
    }
}
