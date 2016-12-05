using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Commands.Parameters
{
    public abstract class LiteralCommandParameterBase : CommandParameterBase
    {
        public LiteralCommandParameterBase()
        {
        }

        public abstract dynamic GetResult();
        public abstract LiteralCommandParameterBase CreateInstance(dynamic value);
    }
}
