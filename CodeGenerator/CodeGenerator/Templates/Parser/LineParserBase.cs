using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Parser
{
    public abstract class LineParserBase : CommandParserBase
    {
        public LineParserBase(IInternalParsable parsable)
            : base(parsable)
        {
        }
    }
}
