// <copyright file="DatabaseDataSourceStrategyTest.cs">Copyright ©  2016</copyright>
using System;
using System.Collections.Generic;
using CodeGenerator.Core.SourceDataStrategies;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeGenerator.Core.SourceDataStrategies.Tests
{
    /// <summary>This class contains parameterized unit tests for DatabaseDataSourceStrategy</summary>
    [PexClass(typeof(DatabaseDataSourceStrategy))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [TestClassAttribute]
    public partial class DatabaseDataSourceStrategyTest
    {
        /// <summary>Test stub for GetAllTables()</summary>
        [PexMethod(MaxBranches = 20000, MaxConditions = 1000)]
        public List<TableDefinition> GetAllTablesTest([PexAssumeUnderTest] DatabaseDataSourceStrategy target)
        {
            List<TableDefinition> result = target.GetAllTables();
            return result;
            // TODO: add assertions to method DatabaseDataSourceStrategyTest.GetAllTablesTest(DatabaseDataSourceStrategy)
        }

        /// <summary>Test stub for .ctor()</summary>
        [PexMethod]
        public DatabaseDataSourceStrategy ConstructorTest()
        {
            DatabaseDataSourceStrategy target = new DatabaseDataSourceStrategy();
            return target;
            // TODO: add assertions to method DatabaseDataSourceStrategyTest.ConstructorTest()
        }



        /// <summary>Test stub for GetColumnsForTable(String)</summary>
        [PexMethod(MaxConstraintSolverTime = 2)]
        public List<ColumnDefinition> GetColumnsForTableTest([PexAssumeUnderTest] DatabaseDataSourceStrategy target, string table)
        {
            List<ColumnDefinition> result = target.GetColumnsForTable(table);
            return result;
            // TODO: add assertions to method DatabaseDataSourceStrategyTest.GetColumnsForTableTest(DatabaseDataSourceStrategy, String)
        }

        /// <summary>Test stub for GetIndexes(String)</summary>
        [PexMethod(MaxConstraintSolverTime = 2)]
        public Dictionary<string, List<string>> GetIndexesTest([PexAssumeUnderTest] DatabaseDataSourceStrategy target, string tableName)
        {
            Dictionary<string, List<string>> result = target.GetIndexes(tableName);
            return result;
            // TODO: add assertions to method DatabaseDataSourceStrategyTest.GetIndexesTest(DatabaseDataSourceStrategy, String)
        }

        /// <summary>Test stub for GetIsColumnIndex(List`1&lt;String&gt;, ColumnDefinition)</summary>
        [PexMethod(MaxRunsWithoutNewTests = 200)]
        public void GetIsColumnIndexTest(
        [PexAssumeUnderTest] DatabaseDataSourceStrategy target,
            List<string> uniqueKeys,
            ColumnDefinition colDef
        )
        {
            target.GetIsColumnIndex(uniqueKeys, colDef);
            // TODO: add assertions to method DatabaseDataSourceStrategyTest.GetIsColumnIndexTest(DatabaseDataSourceStrategy, List`1<String>, ColumnDefinition)
        }

        /// <summary>Test stub for GetReferencedTablesAndIndexes(String, TableDefinition, List`1&lt;String&gt;)</summary>
        [PexMethod(MaxConstraintSolverTime = 2)]
        public void GetReferencedTablesAndIndexesTest(
        [PexAssumeUnderTest] DatabaseDataSourceStrategy target,
            string tableName,
            TableDefinition tableDef,
            List<string> uniqueKeys
        )
        {
            target.GetReferencedTablesAndIndexes(tableName, tableDef, uniqueKeys);
            // TODO: add assertions to method DatabaseDataSourceStrategyTest.GetReferencedTablesAndIndexesTest(DatabaseDataSourceStrategy, String, TableDefinition, List`1<String>)
        }

        /// <summary>Test stub for GetReferencedTablesForColumn(String, String)</summary>
        [PexMethod(MaxConstraintSolverTime = 2)]
        public List<ReferencedTable> GetReferencedTablesForColumnTest(
        [PexAssumeUnderTest] DatabaseDataSourceStrategy target,
            string tableName,
            string columnName
        )
        {
            List<ReferencedTable> result = target.GetReferencedTablesForColumn(tableName, columnName);
            return result;
            // TODO: add assertions to method DatabaseDataSourceStrategyTest.GetReferencedTablesForColumnTest(DatabaseDataSourceStrategy, String, String)
        }

        /// <summary>Test stub for GetTableNames()</summary>
        [PexMethod(MaxConstraintSolverTime = 2)]
        public List<string> GetTableNamesTest([PexAssumeUnderTest] DatabaseDataSourceStrategy target)
        {
            List<string> result = target.GetTableNames();
            return result;
            // TODO: add assertions to method DatabaseDataSourceStrategyTest.GetTableNamesTest(DatabaseDataSourceStrategy)
        }

        /// <summary>Test stub for GetUniqueKeys(TableDefinition)</summary>
        [PexMethod]
        public List<string> GetUniqueKeysTest([PexAssumeUnderTest] DatabaseDataSourceStrategy target, TableDefinition tableDef)
        {
            List<string> result = target.GetUniqueKeys(tableDef);
            return result;
            // TODO: add assertions to method DatabaseDataSourceStrategyTest.GetUniqueKeysTest(DatabaseDataSourceStrategy, TableDefinition)
        }
    }
}
