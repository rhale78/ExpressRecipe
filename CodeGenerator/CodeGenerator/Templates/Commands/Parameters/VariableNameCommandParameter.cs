using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Commands.Parameters
{
    public class VariableNameCommandParameter : StringLiteralCommandParameter
    {
        public VariableNameCommandParameter()
            : base()
        {
        }

        public VariableNameCommandParameter(string value)
            : base(value)
        {
        }

        public override LiteralCommandParameterBase CreateInstance(dynamic value)
        {
            return new VariableNameCommandParameter(value);
        }
    }
}
