using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Commands.Parameters
{
    public class IntLiteralCommandParameter : LiteralCommandParameterBase<int>
    {
        public IntLiteralCommandParameter()
            : base()
        {
        }

        public IntLiteralCommandParameter(int value)
            : base(value)
        {
        }
        public IntLiteralCommandParameter(string value)
        {
            Value = int.Parse(value);
        }
        public override LiteralCommandParameterBase CreateInstance(dynamic value)
        {
            return new IntLiteralCommandParameter(value);
        }
    }
}
