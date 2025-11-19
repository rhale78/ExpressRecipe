using CodeGenerator.Core.Templates.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Parser
{
    public class BlockParser : CommandParserBase
    {
        public BlockParser(IInternalParsable parsable)
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
