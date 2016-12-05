using CodeGenerator.Core.Templates.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Parser
{
    public interface IInternalParsable
    {
        bool CanParseInternal(string[] parts);
        List<CommandParameterBase> ParseInternal(string[] parts);
    }
}
