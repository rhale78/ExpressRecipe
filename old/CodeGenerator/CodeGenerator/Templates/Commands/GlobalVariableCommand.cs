using CodeGenerator.Core.Templates.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using CodeGenerator.Core.Templates.Parser;

namespace CodeGenerator.Core.Templates.Commands
{
    public class GlobalVariableCommand : VariableCommandBase<GlobalVariableCommand>
    {
        //protected override List<CommandParameterBase> ValidParameterTypes {
        //    get { throw new NotImplementedException(); }
        //}

        public GlobalVariableCommand()
        {
        }

        public override void Create(dynamic value)
        {
            Interpreter.SetGlobalVariable(Name, VariableType, value);
        }

        public override void Set(dynamic value)
        {
            Interpreter.SetGlobalVariable(Name, value);
        }

        public override dynamic Get()
        {
            return Interpreter.GetGlobalVariableValue(Name);
        }

        protected override void AssignParameters(List<CommandParameterBase> commandParameters)
        {
            throw new NotImplementedException();
        }

        public override bool CanParseInternal(string[] parts)
        {
            throw new NotImplementedException();
        }

        public override List<CommandParameterBase> ParseInternal(string[] parts)
        {
            throw new NotImplementedException();
        }

        public override CommandParserBase GetParser()
        {
            throw new NotImplementedException();
        }
    }
}
