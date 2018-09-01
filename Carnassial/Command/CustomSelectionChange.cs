using Carnassial.Data;

namespace Carnassial.Command
{
    internal class CustomSelectionChange : UndoableCommand<CarnassialWindow>
    {
        private readonly CustomSelection newSelection;
        private readonly CustomSelection previousSelection;

        public CustomSelectionChange(CustomSelection previousSelection, CustomSelection currentSelection)
        {
            this.IsExecuted = true;
            this.newSelection = new CustomSelection(currentSelection);
            this.previousSelection = previousSelection;
        }

        public override void Execute(CarnassialWindow carnassial)
        {
            carnassial.DataHandler.FileDatabase.CustomSelection = this.newSelection;
            this.IsExecuted = true;
        }

        public bool HasChanges()
        {
            return this.previousSelection.Equals(this.newSelection) == false;
        }

        public override string ToString()
        {
            return "custom selection edit";
        }

        public override void Undo(CarnassialWindow carnassial)
        {
            carnassial.DataHandler.FileDatabase.CustomSelection = this.previousSelection;
            this.IsExecuted = false;
        }
    }
}
