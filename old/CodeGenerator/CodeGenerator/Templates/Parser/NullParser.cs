using CodeGenerator.Core.Templates.Commands.Parameters;
using System.Collections.Generic;
using System;

namespace CodeGenerator.Core.Templates.Parser
{
    public class NullParser : CommandParserBase
    {
        public NullParser(IInternalParsable parsable)
            : base(parsable)
        {
        }

        public override bool CanParse(string line)
        {
            return string.IsNullOrEmpty(line);
        }

        public override List<CommandParameterBase> Parse(string line)
        {
            return new List<CommandParameterBase>();
        }
    }
}
