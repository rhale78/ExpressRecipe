using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Parser
{
    public interface IParsable
    {
        CommandParserBase GetParser();
    }
}
