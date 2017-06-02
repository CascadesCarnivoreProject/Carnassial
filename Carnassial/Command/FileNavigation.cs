using Carnassial.Data;
using System.Threading.Tasks;

namespace Carnassial.Command
{
    internal class FileNavigation : UndoableCommandAsync<CarnassialWindow>
    {
        private int newFileIndex;
        private int previousFileIndex;

        public FileNavigation(FileTableEnumerator fileEnumerator, int previousFileIndex)
        {
            this.IsExecuted = true;
            this.newFileIndex = fileEnumerator.CurrentRow;
            this.previousFileIndex = previousFileIndex;
        }

        public override bool CanExecute(CarnassialWindow carnassial)
        {
            return (this.IsExecuted == false) && carnassial.DataHandler.FileDatabase.IsFileRowInRange(this.newFileIndex);
        }

        public override bool CanUndo(CarnassialWindow carnassial)
        {
            return this.IsExecuted && carnassial.DataHandler.FileDatabase.IsFileRowInRange(this.previousFileIndex);
        }

        public async override Task ExecuteAsync(CarnassialWindow carnassial)
        {
            await carnassial.ShowFileAsync(this.newFileIndex, false);
            this.IsExecuted = true;
        }

        public override string ToString()
        {
            return "navigation to file " + this.newFileIndex;
        }

        public async override Task UndoAsync(CarnassialWindow carnassial)
        {
            await carnassial.ShowFileAsync(this.previousFileIndex, false);
            this.IsExecuted = false;
        }
    }
}
