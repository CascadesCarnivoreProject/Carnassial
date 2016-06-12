using System.Data;

namespace Timelapse.Database
{
    public class ImageDataNoteColumn : ImageDataColumn
    {
        public ImageDataNoteColumn(DataRow templateTableRow)
            : base(templateTableRow)
        {
        }

        public override bool IsContentValid(string value)
        {
            return true;
        }
    }
}
