using CodeGenerator.Core.Templates.Commands.Parameters;
using System.Collections.Generic;
using System;

namespace CodeGenerator.Core.Templates.Parser
{
    public class RegexParser : CommandParserBase
    {
        public RegexParser(IInternalParsable parsable)
            : base(parsable)
        {
        }

        public override bool CanParse(string line)
        {
            throw new NotImplementedException();
        }

        public override List<CommandParameterBase> Parse(string line)
        {
            throw new NotImplementedException();
        }
    }
}
