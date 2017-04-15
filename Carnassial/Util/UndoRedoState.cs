using Carnassial.Data;
using System.Collections.Generic;

namespace Carnassial.Util
{
    public class UndoRedoState
    {
        public UndoRedoType Type { get; private set; }
        public Dictionary<string, object> Values { get; private set; }

        public UndoRedoState(ImageRow file)
        {
            this.Type = UndoRedoType.FileValues;
            this.Values = file.AsDictionary();
        }

        public override bool Equals(object obj)
        {
            if (obj is UndoRedoState == false)
            {
                return false;
            }

            UndoRedoState other = (UndoRedoState)obj;
            if (this.Values.Count != other.Values.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, object> keyValue in this.Values)
            {
                object otherValue;
                if (other.Values.TryGetValue(keyValue.Key, out otherValue) == false)
                {
                    return false;
                }

                if ((keyValue.Value == null) && (otherValue == null))
                {
                    continue;
                }
                if ((keyValue.Value == null) ^ (otherValue == null))
                {
                    return false;
                }
                if (keyValue.Value.Equals(otherValue) == false)
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            return this.Values.GetHashCode();
        }
    }
}
