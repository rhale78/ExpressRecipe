using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.SourceDataStrategies
{
    public class TableDefinition
    {
        public string TableName { get; set; }
        public List<ColumnDefinition> Columns { get; set; }
        public Dictionary<string, List<string>> Indexes { get; set; }
    }
}
