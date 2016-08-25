namespace Timelapse.Database
{
    public class ImageDataNoteColumn : ImageDataColumn
    {
        public ImageDataNoteColumn(ControlRow control)
            : base(control)
        {
        }

        public override bool IsContentValid(string value)
        {
            return true;
        }
    }
}
