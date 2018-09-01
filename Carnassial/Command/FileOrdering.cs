using Carnassial.Data;
using System.Threading.Tasks;

namespace Carnassial.Command
{
    internal class FileOrdering : UndoableCommandAsync<CarnassialWindow>
    {
        private readonly long originalFileID;

        public FileOrdering(FileTableEnumerator fileEnumerator)
        {
            this.originalFileID = fileEnumerator.GetCurrentFileID();
        }

        public override async Task ExecuteAsync(CarnassialWindow carnassial)
        {
            this.ToggleOrdering(carnassial);
            await carnassial.SelectFilesAndShowFileAsync();
            this.IsExecuted = true;
        }

        private void ToggleOrdering(CarnassialWindow carnassial)
        {
            carnassial.State.OrderFilesByDateTime = !carnassial.State.OrderFilesByDateTime;
            carnassial.DataHandler.FileDatabase.OrderFilesByDateTime = carnassial.State.OrderFilesByDateTime;
            carnassial.MenuOptionsOrderFilesByDateTime.IsChecked = carnassial.State.OrderFilesByDateTime;
        }

        public override string ToString()
        {
            return "file ordering";
        }

        public override async Task UndoAsync(CarnassialWindow carnassial)
        {
            this.ToggleOrdering(carnassial);
            await carnassial.SelectFilesAndShowFileAsync(this.originalFileID, carnassial.DataHandler.FileDatabase.ImageSet.FileSelection);
            this.IsExecuted = false;
        }
    }
}
