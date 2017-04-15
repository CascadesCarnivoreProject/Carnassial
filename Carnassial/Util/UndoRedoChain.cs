using Carnassial.Data;
using System.Collections.Generic;

namespace Carnassial.Util
{
    public class UndoRedoChain
    {
        private LinkedList<UndoRedoState> chain;
        private LinkedListNode<UndoRedoState> position;

        public UndoRedoChain()
        {
            this.chain = new LinkedList<UndoRedoState>();
            this.position = null;
        }

        public bool CanRedo
        {
            get { return (this.position != null) && (this.position.Next != null); }
        }

        public bool CanUndo
        {
            // when a single state is available a redo will be inserted on calling GetUndo()
            // In this case this.position's not decremented, so a position of 0 indicates undo availability.
            get { return (this.position != null) && ((this.position.Previous != null) || (this.chain.Count == 1)); }
        }

        private LinkedListNode<UndoRedoState> AddLast(UndoRedoState state)
        {
            // add
            LinkedListNode<UndoRedoState> last = this.chain.AddLast(state);

            // limit memory footprint of chain
            if (this.chain.Count > Constant.MaximumUndoRedoStates)
            {
                this.chain.RemoveFirst();
            }

            return last;
        }

        public void AddStateIfDifferent(ImageRow file)
        {
            UndoRedoState state = new UndoRedoState(file);
            if (this.position != null && this.position.Value.Equals(state))
            {
                // nothing to do; states are the same
                return;
            }

            // remove any available redo chain as it's invalidated by the incoming undo
            if (this.CanRedo)
            {
                for (LinkedListNode<UndoRedoState> nodeToRemove = this.chain.Last; nodeToRemove != this.position; nodeToRemove = this.chain.Last)
                {
                    this.chain.RemoveLast();
                }
            }

            // add new undo item
            this.position = this.AddLast(state);
        }

        public void Clear()
        {
            this.chain.Clear();
            this.position = null;
        }

        public bool TryGetRedo(out UndoRedoState state)
        {
            if (this.CanRedo == false)
            {
                state = null;
                return false;
            }

            this.position = this.position.Next;
            state = this.position.Value;
            return true;
        }

        public bool TryGetUndo(out UndoRedoState state)
        {
            if (this.CanUndo == false)
            {
                state = null;
                return false;
            }

            if (this.position.Previous != null)
            {
                this.position = this.position.Previous;
            }
            state = this.position.Value;
            return true;
        }
    }
}
