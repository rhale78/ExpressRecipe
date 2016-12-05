using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Commands.Parameters
{
    public class DoubleLiteralCommandParameter : LiteralCommandParameterBase<double>
    {
        public DoubleLiteralCommandParameter()
            : base()
        {
        }

        public DoubleLiteralCommandParameter(double value)
            : base(value)
        {
        }
        public DoubleLiteralCommandParameter(string value)
        {
            Value = double.Parse(value);
        }
        public override LiteralCommandParameterBase CreateInstance(dynamic value)
        {
            return new DoubleLiteralCommandParameter(value);
        }
    }
}
