using System.Collections.Generic;

namespace Carnassial.Database
{
    public class ImageDataChoiceColumn : ImageDataColumn
    {
        private List<string> choices;
        private string defaultValue;

        public ImageDataChoiceColumn(ControlRow control)
            : base(control)
        {
            this.choices = control.GetChoices();
            this.defaultValue = control.DefaultValue;
        }

        public override bool IsContentValid(string value)
        {
            // the editor doesn't currently enforce the default value is one of the choices, so accept it as valid independently
            if (value == this.defaultValue)
            {
                return true;
            }
            return this.choices.Contains(value);
        }
    }
}
