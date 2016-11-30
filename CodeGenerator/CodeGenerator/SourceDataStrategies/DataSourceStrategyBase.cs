using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.SourceDataStrategies
{
    public abstract class DataSourceStrategyBase
    {
        public string Settings { get; set; }

        //protected List<TableDefinition> Tables { get; set; }
        public abstract List<TableDefinition> GetAllTables();

    }
}
