﻿using CodeGenerator.Core.SourceDataStrategies;
using CodeGenerator.Core.Templates.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            //DatabaseDataSourceStrategy databaseDataSourceStrategy = new DatabaseDataSourceStrategy();
            //((DataSourceStrategyBase)databaseDataSourceStrategy).Settings = "Server=RHALE78-5-1102;Database=ExpressRecipe.Logging;Trusted_Connection=True;";
            //databaseDataSourceStrategy.GetAllTables();

            CommandInterpreter interpreter = new CommandInterpreter();
            interpreter.SetVariable("test", "string", "test123");
            interpreter.SetVariable("test", "123");
            dynamic tmp=interpreter.GetVariableValue("test");
            int a = 0;
        }
    }
}