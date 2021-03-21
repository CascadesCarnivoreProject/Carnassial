using Carnassial.Data;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Carnassial.Command
{
    internal class FileNavigation : UndoableCommandAsync<CarnassialWindow>
    {
        private readonly int newFileIndex;
        private readonly int previousFileIndex;

        public FileNavigation(FileTableEnumerator fileEnumerator, int previousFileIndex)
        {
            this.IsExecuted = true;
            this.newFileIndex = fileEnumerator.CurrentRow;
            this.previousFileIndex = previousFileIndex;
        }

        public override bool CanExecute(CarnassialWindow carnassial)
        {
            Debug.Assert(carnassial.IsFileDatabaseAvailable());
            return (this.IsExecuted == false) && carnassial.DataHandler.FileDatabase.IsFileRowInRange(this.newFileIndex);
        }

        public override bool CanUndo(CarnassialWindow carnassial)
        {
            Debug.Assert(carnassial.IsFileDatabaseAvailable());
            return this.IsExecuted && carnassial.DataHandler.FileDatabase.IsFileRowInRange(this.previousFileIndex);
        }

        public async override Task ExecuteAsync(CarnassialWindow carnassial)
        {
            await carnassial.ShowFileAsync(this.newFileIndex, false).ConfigureAwait(true);
            this.IsExecuted = true;
        }

        public override string ToString()
        {
            return "navigation to file " + this.newFileIndex;
        }

        public async override Task UndoAsync(CarnassialWindow carnassial)
        {
            await carnassial.ShowFileAsync(this.previousFileIndex, false).ConfigureAwait(true);
            this.IsExecuted = false;
        }
    }
}
