using CodeGenerator.Core.Templates.Commands.Parameters;
using System.Collections.Generic;
using System;

namespace CodeGenerator.Core.Templates.Parser
{
    public class FallbackParser : CommandParserBase
    {
        public FallbackParser(IInternalParsable parsable)
            : base(parsable)
        {
        }

        public override bool CanParse(string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                return CanParseInternal(new string[]
                { line
                });
            }
            return false;
        }

        public override List<CommandParameterBase> Parse(string line)
        {
            line = line.Trim();
            return ParseInternal(new string[]
            { line
            });
        }
    }
}
