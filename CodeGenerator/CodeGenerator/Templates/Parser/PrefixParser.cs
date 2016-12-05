using CodeGenerator.Core.Templates.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Parser
{
    public class PrefixParser : CommandParserBase
    {
        public PrefixParser(IInternalParsable parsable, string prefix)
            : base(parsable)
        {
            Prefix = prefix;
        }

        public string Prefix { get; protected set; }

        public override bool CanParse(string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                line = line.Trim();
                if (line.StartsWith(Prefix))
                {
                    line = GetLineWithoutToken(line);
                    return CanParseInternal(new string[]
                    { line
                    });
                }
            }
            return false;
        }

        protected string GetLineWithoutToken(string line)
        {
            return line.Replace(Prefix, string.Empty).Trim();
        }

        public override List<CommandParameterBase> Parse(string line)
        {
            line = GetLineWithoutToken(line);
            //RSH 12/2/16 - split via comma? - will have to test all parameters first before doing it otherwise will get "(a", "b", "c)" instead of Parenthesis(a,b,c)
            return ParseInternal(new string[]
            { line
            });
        }
    }
}
