using CodeGenerator.Core.CodeFiles;
using CodeGenerator.Core.SourceDataStrategies;
using CodeGenerator.Core.Templates.Commands;
using CodeGenerator.Core.Templates.Parser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Interpreter
{
    public class CommandInterpreter
    {
        protected VariableStack VariableStack { get; set; }
        public CodeFileBase CodeFile { get; protected set; }

        public CommandInterpreter()
        {
            VariableStack = new VariableStack();
        }

        public void Run(DataSourceStrategyBase dataSource, List<TemplateBase> templates)
        {
            List<TableDefinition> tables = dataSource.GetAllTables();

            foreach (TemplateBase template in templates)
            {
                Run(tables, template);
            }
        }

        private void Run(List<TableDefinition> tables, TemplateBase template)
        {
            if (template is StaticTemplate)
            {
                Run(tables, (StaticTemplate)template);
            }
            else
            {
                if (template is DynamicTemplate)
                {
                    Run(tables, (DynamicTemplate)template);
                }
            }
        }

        private void Run(List<TableDefinition> tables, StaticTemplate template)
        {
        }
        private void Run(List<TableDefinition> tables, DynamicTemplate template)
        {
            Commands = TemplateParser.Parse(template);
            foreach (TableDefinition table in tables)
            {
                SetGlobalVariable("TableName", "string", table.TableName);

                CodeFile = CodeFileFactory.Create(template.FileType);

                foreach (CommandBase command in Commands)
                {
                    command.Execute();
                }
            }
        }

        protected List<CommandBase> Commands { get; set; }

        public string Execute(string line)
        {
            return null;
        }

        public void CreateStackFrame()
        {
            VariableStack.CreateStackFrame();
        }
        public void DestroyStackFrame()
        {
            VariableStack.DestroyStackFrame();
        }
        public void SetVariable(string name, string type, dynamic value)
        {
            VariableStack.SetVariable(name, type, value);
        }
        public void SetVariable(string name, dynamic value)
        {
            VariableStack.SetVariable(name,  value);
        }
        public dynamic GetVariableValue(string name)
        {
            return VariableStack.GetVariableValue(name);
        }
        public VariableBase GetVariable(string name)
        {
            return VariableStack.GetVariable(name);
        }
        public void SetGlobalVariable(string name, string type, dynamic value)
        {
            VariableStack.SetGlobalVariable(name, type, value);
        }
        public void SetGlobalVariable(string name, dynamic value)
        {
            VariableStack.SetGlobalVariable(name, value);
        }
        public dynamic GetGlobalVariableValue(string name)
        {
            return VariableStack.GetGlobalVariableValue(name);
        }
        public VariableBase GetGlobalVariable(string name)
        {
            return VariableStack.GetGlobalVariable(name);
        }

        internal bool HasVariable(string name)
        {
            return VariableStack.HasVariable(name);
        }
    }
}
