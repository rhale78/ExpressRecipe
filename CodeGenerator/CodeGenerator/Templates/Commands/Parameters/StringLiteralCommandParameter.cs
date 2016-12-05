using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Commands.Parameters
{
    public class StringLiteralCommandParameter : LiteralCommandParameterBase<string>
    {
        public StringLiteralCommandParameter()
            : base()
        {
        }

        public StringLiteralCommandParameter(string value)
            : base(value)
        {
        }

        public override LiteralCommandParameterBase CreateInstance(dynamic value)
        {
            return new StringLiteralCommandParameter(value);
        }
    }
}
