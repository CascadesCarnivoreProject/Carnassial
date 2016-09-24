namespace Carnassial.Database
{
    public class ImageDataUtcOffsetColumn : ImageDataColumn
    {
        public ImageDataUtcOffsetColumn(ControlRow control)
            : base(control)
        {
        }

        public override bool IsContentValid(string value)
        {
            double utcOffset;
            return double.TryParse(value, out utcOffset);
        }
    }
}
