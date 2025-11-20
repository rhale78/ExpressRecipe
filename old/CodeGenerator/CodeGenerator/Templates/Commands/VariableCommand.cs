using CodeGenerator.Core.Templates.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using CodeGenerator.Core.Templates.Parser;

namespace CodeGenerator.Core.Templates.Commands
{
    public class VariableCommand : VariableCommandBase<VariableCommand>
    {
        //protected override List<CommandParameterBase> ValidParameterTypes {
        //    get { throw new NotImplementedException(); }
        //}

        public VariableCommand()
        {
        }

        public override void Create(dynamic value)
        {
            Interpreter.SetVariable(Name, VariableType, value);
        }

        public override void Set(dynamic value)
        {
            Interpreter.SetVariable(Name, value);
        }

        public override dynamic Get()
        {
            return Interpreter.GetVariableValue(Name);
        }

        protected override void AssignParameters(List<CommandParameterBase> commandParameters)
        {
            throw new NotImplementedException();
        }

        public override bool CanParseInternal(string[] parts)
        {
            if (parts != null)
            {
                if (parts.Length == 1)
                {
                    return true;
                }
            }
            return false;
        }

        public override List<CommandParameterBase> ParseInternal(string[] parts)
        {
            throw new NotImplementedException();
        }

        public override CommandParserBase GetParser()
        {
            return new RegexParser(this);
        }
    }
}
