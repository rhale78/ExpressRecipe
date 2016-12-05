using CodeGenerator.Core.Templates.Commands.Parameters;
using System;
using System.Collections.Generic;

namespace CodeGenerator.Core.Templates.Parser
{
    public class InfixParser : CommandParserBase
    {
        public InfixParser(IInternalParsable parsable, string infix)
            : base(parsable)
        {
            Infix = infix;
        }

        public string Infix { get; protected set; }

        public override bool CanParse(string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                if (line.Contains(Infix))
                {
                    string[] lineParts = GetLineParts(line);
                    return CanParseInternal(lineParts);
                }
            }
            return false;
        }

        protected virtual string[] GetLineParts(string line)
        {
            return line.Split(new string[]
            { Infix
            }, StringSplitOptions.RemoveEmptyEntries);
        }

        public override List<CommandParameterBase> Parse(string line)
        {
            line = line.Trim();
            string[] lineParts = GetLineParts(line);
            return ParseInternal(lineParts);
        }
    }
}
