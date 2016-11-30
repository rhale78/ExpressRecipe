using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.SourceDataStrategies
{
    public class TableDefinition
    {
        public string TableName { get; set; }
        public List<ColumnDefinition> Columns { get; set; }
        public Dictionary<string,List<string>> Indexes { get; set; }
    }
}
