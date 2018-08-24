using System.Collections.Generic;
using System.Data.SQLite;

namespace Carnassial.Database
{
    public abstract class WindowedTransactionSequence<TInput> : TransactionSequence
    {
        public WindowedTransactionSequence(SQLiteConnection connection)
            : base(connection)
        {
        }

        public abstract int AddToSequence(IList<TInput> inputs, int offset, int length);
    }
}
