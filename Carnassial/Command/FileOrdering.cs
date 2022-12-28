using Carnassial.Data;
using System.Diagnostics;
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
            await carnassial.SelectFilesAndShowFileAsync().ConfigureAwait(true);
            this.IsExecuted = true;
        }

        private void ToggleOrdering(CarnassialWindow carnassial)
        {
            Debug.Assert(carnassial.IsFileDatabaseAvailable());

            bool orderFilesByDateTime = !CarnassialSettings.Default.OrderFilesByDateTime;
            CarnassialSettings.Default.OrderFilesByDateTime = orderFilesByDateTime;
            carnassial.DataHandler.FileDatabase.OrderFilesByDateTime = orderFilesByDateTime;
            carnassial.MenuOptionsOrderFilesByDateTime.IsChecked = orderFilesByDateTime;
        }

        public override string ToString()
        {
            return "file ordering";
        }

        public override async Task UndoAsync(CarnassialWindow carnassial)
        {
            Debug.Assert(carnassial.IsFileDatabaseAvailable());

            this.ToggleOrdering(carnassial);
            await carnassial.SelectFilesAndShowFileAsync(this.originalFileID, carnassial.DataHandler.FileDatabase.ImageSet.FileSelection).ConfigureAwait(true);
            this.IsExecuted = false;
        }
    }
}
