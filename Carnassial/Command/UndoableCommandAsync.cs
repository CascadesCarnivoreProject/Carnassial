using System.ComponentModel;
using System.Threading.Tasks;

namespace Carnassial.Command
{
    public abstract class UndoableCommandAsync<TParameter> : UndoableCommand<TParameter>
    {
        public override bool IsAsync
        {
            get { return true; }
        }

        [EditorBrowsableAttribute(EditorBrowsableState.Never)]
        public override void Execute(TParameter parameter)
        {
            this.ExecuteAsync(parameter).Wait();
        }

        public abstract Task ExecuteAsync(TParameter parameter);

        [EditorBrowsableAttribute(EditorBrowsableState.Never)]
        public override void Undo(TParameter parameter)
        {
            this.UndoAsync(parameter).Wait();
        }

        public abstract Task UndoAsync(TParameter parameter);
    }
}
