using CodeGenerator.Core.Templates.Commands.Parameters;
using System.Collections.Generic;
using System;

namespace CodeGenerator.Core.Templates.Parser
{
    public class BoolParser : CommandParserBase
    {
        public BoolParser(IInternalParsable parsable)
            : base(parsable)
        {
        }

        public override bool CanParse(string line)
        {
            bool result;
            return bool.TryParse(line, out  result);
        }

        public override List<CommandParameterBase> Parse(string line)
        {
            return new List<CommandParameterBase>
            { new BoolLiteralCommandParameter(line)
            };
        }
    }
}
