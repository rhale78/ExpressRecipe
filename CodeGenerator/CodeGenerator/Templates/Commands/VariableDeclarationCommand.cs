using CodeGenerator.Core.Templates.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using CodeGenerator.Core.Templates.Parser;

namespace CodeGenerator.Core.Templates.Commands
{
    public abstract class VariableDeclarationCommand<T> : CommandBase<T>
        where T: VariableDeclarationCommand<T>, new()
    {
        protected string VariableName { get; set; }
        protected VariableNameCommandParameter VariableParameter { get; set; }

        public override bool CanParseInternal(string[] parts)
        {
            if (parts != null)
            {
                if (parts.Length == 1)
                {
                    string part = parts[0];
                    VariableCommand varCmd = new VariableCommand();
                    return varCmd.CanParse(part);
                }
            }
            return false;
        }

        public override List<CommandParameterBase> ParseInternal(string[] parts)
        {
            return new List<CommandParameterBase>()
            { new VariableNameCommandParameter(parts[0])
            };
        }

        protected override void AssignParameters(List<CommandParameterBase> commandParameters)
        {
            VariableName = ((VariableNameCommandParameter)commandParameters[0]).GetResult();
            VariableParameter = (VariableNameCommandParameter)commandParameters[0];
        }
    }
}
