using Carnassial.Images;
using System;

namespace Carnassial.Dialog
{
    public class DateTimeRereadResult : MetadataFieldResult
    {
        public static new readonly DateTimeRereadResult Default = new DateTimeRereadResult();

        private DateTimeRereadResult()
            : base(null, null)
        {
        }

        public DateTimeRereadResult(FileLoad fileLoad, DateTimeOffset originalDateTime)
            : base(fileLoad.FileName)
        {
            if (fileLoad.MetadataReadResult == MetadataReadResult.None)
            {
                this.Message = "Read file date/time: ";
            }
            else if (fileLoad.MetadataReadResult == MetadataReadResult.DateTimeInferredFromPrevious)
            {
                this.Message = "Read previous metadata date/time: ";
            }
            else
            {
                this.Message = "Read metadata date/time: ";
            }

            DateTimeOffset reloadedDateTimeOffset = fileLoad.File.DateTimeOffset;
            if (reloadedDateTimeOffset == originalDateTime)
            {
                this.Message += "no change.";
            }
            else
            {
                if (reloadedDateTimeOffset.Date == originalDateTime.Date)
                {
                    this.Message += "same date, ";
                }
                else
                {
                    this.Message += "different date, ";
                }

                if (reloadedDateTimeOffset.TimeOfDay == originalDateTime.TimeOfDay)
                {
                    this.Message += "same time, ";
                }
                else
                {
                    this.Message += "different time, ";
                }

                if (reloadedDateTimeOffset.Offset == originalDateTime.Offset)
                {
                    this.Message += "same UTC offset.";
                }
                else
                {
                    this.Message += "different UTC offset.";
                }
            }
        }
    }
}
