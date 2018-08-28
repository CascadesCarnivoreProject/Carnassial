using System.Collections.Generic;

namespace Carnassial.Database
{
    public abstract class WindowedTransactionSequence<TInput> : TransactionSequence
    {
        public WindowedTransactionSequence(SQLiteDatabase database)
            : base(database)
        {
        }

        public abstract int AddToSequence(IList<TInput> inputs, int offset, int length);
    }
}
