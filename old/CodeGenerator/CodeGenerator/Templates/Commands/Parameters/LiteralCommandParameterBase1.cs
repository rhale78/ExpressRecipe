using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Commands.Parameters
{
    public abstract class LiteralCommandParameterBase<T> : LiteralCommandParameterBase
    {
        public LiteralCommandParameterBase()
            : base()
        {
        }

        public LiteralCommandParameterBase(T value)
        {
            Value = value;
        }

        protected T Value { get; set; }

        public override dynamic GetResult()
        {
            return Value;
        }
    }
}
