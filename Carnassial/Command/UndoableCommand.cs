using System;
using System.Windows.Input;

namespace Carnassial.Command
{
    public abstract class UndoableCommand<TParameter> : ICommand
    {
        public event EventHandler CanExecuteChanged;

        public bool IsExecuted { get; protected set; }

        public UndoableCommand()
        {
            this.IsExecuted = false;
        }

        public virtual bool IsAsync
        {
            get { return false; }
        }

        public virtual bool CanExecute(TParameter parameter)
        {
            return this.IsExecuted == false;
        }

        public virtual bool CanUndo(TParameter parameter)
        {
            return this.IsExecuted;
        }

        public abstract void Execute(TParameter parameter);

        bool ICommand.CanExecute(object parameter)
        {
            return this.CanExecute((TParameter)parameter);
        }

        void ICommand.Execute(object parameter)
        {
            this.Execute((TParameter)parameter);
        }

        public abstract void Undo(TParameter parameter);
    }
}
