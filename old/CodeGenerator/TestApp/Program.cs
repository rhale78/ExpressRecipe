using CodeGenerator.Core.Templates;
using CodeGenerator.Core.Templates.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestApp
{
    internal class Program
    {
        private static void Main(string[] args)
        {
			//DatabaseDataSourceStrategy databaseDataSourceStrategy = new DatabaseDataSourceStrategy();
			//((DataSourceStrategyBase)databaseDataSourceStrategy).Settings = "Server=RHALE78-5-1102;Database=ExpressRecipe.Logging;Trusted_Connection=True;";
			//databaseDataSourceStrategy.GetAllTables();

			DynamicTemplate template = new DynamicTemplate();
			template.LoadSingleLine("this is a test");
			template.LoadSingleLine("!@this is a test@!");
			template.LoadSingleLine("!@this@! is a test");
			template.LoadSingleLine("!@this@! is a !@test@!");
			template.LoadSingleLine("@!this@! is !@a !@test");
			template.LoadSingleLine("!@this!@ is @!a @!test");

			CommandInterpreter interpreter = new CommandInterpreter();
            interpreter.SetVariable("test", "string", "test123");
            interpreter.SetVariable("test", "123");
            dynamic tmp = interpreter.GetVariableValue("test");
            int a = 0;
        }
    }
}
