using System.Collections.Generic;
using System.Data;
using Timelapse.Util;

namespace Timelapse.Database
{
    public class ImageDataChoiceColumn : ImageDataColumn
    {
        private List<string> choices;

        public ImageDataChoiceColumn(DataRow templateTableRow)
            : base(templateTableRow)
        {
            string barSeparatedChoices = templateTableRow.GetStringField(Constants.Control.List);
            this.choices = Utilities.ConvertBarsToList(barSeparatedChoices);
        }

        public override bool IsContentValid(string value)
        {
            return this.choices.Contains(value);
        }
    }
}
