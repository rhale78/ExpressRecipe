using CodeGenerator.Core.CodeFiles;
using CodeGenerator.Core.Templates.Commands.Parameters;
using CodeGenerator.Core.Templates.Interpreter;
using CodeGenerator.Core.Templates.Parser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Commands
{
    public abstract class CommandBase : IInternalParsable, IParsable
    {
        protected CommandInterpreter Interpreter { get; set; }

        protected CodeFileBase CodeFile {
            get { return Interpreter.CodeFile; }
        }

        protected CommandParserBase CommandParser { get; set; }

        public bool CanParse(string line)
        {
            return CommandParser.CanParse(line);
        }

        public void Parse(string line)
        {
            List<CommandParameterBase> commandParameters = CommandParser.Parse(line);
            //ValidateParameters(commandParameters);
            AssignParameters(commandParameters);
        }

        //protected void ValidateParameters(List<CommandParameterBase> commandParameters)
        //{
        //    int index = 0;
        //    //foreach (CommandParameterBase parameter in ValidParameterTypes)
        //    //{
        //    //    if (commandParameters[index] is typeof(parameter))
        //    //    {
        //    //    }
        //    //    else
        //    //    {
        //    //        throw new Exception("Type mismatch");
        //    //    }
        //    //}
        //}

        protected abstract void AssignParameters(List<CommandParameterBase> commandParameters);

        //protected abstract List<CommandParameterBase> ValidParameterTypes { get; }

        public abstract void Execute();
        public abstract bool CanParseInternal(string[] parts);
        public abstract List<CommandParameterBase> ParseInternal(string[] parts);
        public abstract CommandParserBase GetParser();
    }

    public abstract class CommandBase<T> : CommandBase
        where T: CommandBase, new()
    {
        public T CreateInstance()
        {
            return new T();
        }
    }
}
