using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.SourceDataStrategies
{
    public class ColumnDefinition
    {
        public string ColumnName { get; set; }
        public int ColumnIndex { get; set; }
        public int ColumnSize { get; set; }
        public string ColumnType { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsIndex { get; set; }
        public List<ReferencedTable> ReferencedTables { get; set; }
    }
}
