namespace Carnassial.Database
{
    public abstract class SQLiteRow
    {
        public bool HasChanges { get; protected set; }
        public long ID { get; set; }

        public SQLiteRow()
        {
            this.HasChanges = false;
            this.ID = Constant.Database.InvalidID;
        }

        public void AcceptChanges()
        {
            this.HasChanges = false;
        }
    }
}
