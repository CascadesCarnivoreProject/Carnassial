using System.Data.SQLite;

namespace Carnassial.Database
{
    public interface ISQLiteTable
    {
        void Load(SQLiteDataReader reader);
    }
}
