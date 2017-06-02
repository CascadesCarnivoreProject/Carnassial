using Carnassial.Control;
using Carnassial.Data;
using System.Collections.Generic;

namespace Carnassial.Command
{
    public abstract class FileChange : UndoableCommand<CarnassialWindow>
    {
        protected long FileID { get; private set; }

        protected FileChange(long fileID)
        {
            this.FileID = fileID;
        }

        protected void ApplyValueToFile(ImageRow file, string propertyName, object value, DataEntryControl control, Dictionary<string, object> fileSnapshot)
        {
            if (control.Type == ControlType.Note)
            {
                DataEntryNote noteControl = (DataEntryNote)control;
                noteControl.ContentControl.SuppressAutocompletion = true;
                file[propertyName] = value;
                noteControl.ContentControl.SuppressAutocompletion = false;
            }
            else
            {
                file[propertyName] = value;
            }
            fileSnapshot[propertyName] = value;
        }

        public override bool CanExecute(CarnassialWindow carnassial)
        {
            return (this.IsExecuted == false) && (this.FileID == carnassial.DataHandler.ImageCache.GetCurrentFileID());
        }

        public override bool CanUndo(CarnassialWindow carnassial)
        {
            return this.IsExecuted && (this.FileID == carnassial.DataHandler.ImageCache.GetCurrentFileID());
        }
    }
}
