using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Commands.Parameters
{
    public class BoolLiteralCommandParameter : LiteralCommandParameterBase<bool>
    {
        public BoolLiteralCommandParameter()
            : base()
        {
        }

        public BoolLiteralCommandParameter(bool value)
            : base(value)
        {
        }
        public BoolLiteralCommandParameter(string value)
        {
            Value = bool.Parse(value);
        }
        public override LiteralCommandParameterBase CreateInstance(dynamic value)
        {
            return new BoolLiteralCommandParameter(value);
        }
    }
}
