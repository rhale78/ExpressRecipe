using CodeGenerator.Core.Templates.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Parser
{
    public class SurroundParser : CommandParserBase
    {
        public SurroundParser(IInternalParsable parsable)
            : base(parsable)
        {
        }

        public string Prefix { get; protected set; }
        public string Suffix { get; protected set; }

        protected string GetLineWithoutTokens(string line)
        {
            return line.Replace(Prefix, string.Empty).Replace(Suffix, string.Empty).Trim();
        }

        public override bool CanParse(string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                line = line.Trim();
                if (line.StartsWith(Prefix))
                {
                    if (line.EndsWith(Suffix))
                    {
                        line = GetLineWithoutTokens(line);
                        if (!string.IsNullOrEmpty(line))
                        {
                            return CanParseInternal(new string[]
                            { line
                            });
                        }
                    }
                }
            }
            return false;
        }

        public override List<CommandParameterBase> Parse(string line)
        {
            line = GetLineWithoutTokens(line);
            return ParseInternal(new string[]
            { line
            });
        }
    }
}
