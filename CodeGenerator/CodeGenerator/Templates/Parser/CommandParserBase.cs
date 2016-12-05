using CodeGenerator.Core.Templates.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Parser
{
    public abstract class CommandParserBase
    {
        public CommandParserBase(IInternalParsable parsable)
        {
            Parsable = parsable;
        }

        public abstract bool CanParse(string line);
        public abstract List<CommandParameterBase> Parse(string line);

        internal  IInternalParsable Parsable { get; set; }

        protected bool CanParseInternal(string[] parts)
        {
            return Parsable.CanParseInternal(parts);
        }
        protected List<CommandParameterBase> ParseInternal(string[] parts)
        {
            return Parsable.ParseInternal(parts);
        }
    }
}
