using CodeGenerator.Core.Templates.Commands.Parameters;
using System.Collections.Generic;
using System;

namespace CodeGenerator.Core.Templates.Parser
{
    public class IntegerParser : CommandParserBase
    {
        public IntegerParser(IInternalParsable parsable)
            : base(parsable)
        {
        }

        public override bool CanParse(string line)
        {
            if (!line.Contains("."))
            {
                int result;
                return int.TryParse(line, out result);
            }
            return false;
        }

        public override List<CommandParameterBase> Parse(string line)
        {
            return new List<CommandParameterBase>()
            { new IntLiteralCommandParameter(line)
            };
        }
    }
}
