using System.Collections.Generic;

namespace Carnassial.Data
{
    public class AutocompletionCache
    {
        private readonly Dictionary<string, List<string>> autocompletionsByDataLabel;
        private readonly FileDatabase fileDatabase;

        public AutocompletionCache(FileDatabase fileDatabase)
        {
            this.autocompletionsByDataLabel = new Dictionary<string, List<string>>();
            this.fileDatabase = fileDatabase;
        }

        public List<string> this[string dataLabel]
        {
            get
            {
                if (this.autocompletionsByDataLabel.TryGetValue(dataLabel, out List<string> autocompletions))
                {
                    return autocompletions;
                }

                autocompletions = this.fileDatabase.GetDistinctValuesInFileDataColumn(dataLabel);
                this.autocompletionsByDataLabel.Add(dataLabel, autocompletions);
                return autocompletions;
            }
        }
    }
}
