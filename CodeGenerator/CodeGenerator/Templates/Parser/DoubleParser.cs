using CodeGenerator.Core.Templates.Commands.Parameters;
using System.Collections.Generic;
using System;

namespace CodeGenerator.Core.Templates.Parser
{
    public class DoubleParser : CommandParserBase
    {
        public DoubleParser(IInternalParsable parsable)
            : base(parsable)
        {
        }

        public override bool CanParse(string line)
        {
            if (line.Contains("."))
            {
                double result;
                return double.TryParse(line, out result);
            }
            return false;
        }

        public override List<CommandParameterBase> Parse(string line)
        {
            return new List<CommandParameterBase>()
            { new DoubleLiteralCommandParameter(line)
            };
        }
    }
}
