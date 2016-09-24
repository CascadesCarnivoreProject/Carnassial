using Carnassial.Util;
using System.Collections.Generic;

namespace Carnassial.Database
{
    public class ImageDataChoiceColumn : ImageDataColumn
    {
        private List<string> choices;

        public ImageDataChoiceColumn(ControlRow control)
            : base(control)
        {
            this.choices = Utilities.ConvertBarsToList(control.List);
        }

        public override bool IsContentValid(string value)
        {
            return this.choices.Contains(value);
        }
    }
}
