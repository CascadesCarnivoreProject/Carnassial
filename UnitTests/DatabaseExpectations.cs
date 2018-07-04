using System.Collections.ObjectModel;

namespace Carnassial.UnitTests
{
    internal class DatabaseExpectations
    {
        public ReadOnlyCollection<string> ExpectedColumns { get; set; }
        public string FileName { get; set; }
        public string TemplateDatabaseFileName { get; set; }
    }
}
