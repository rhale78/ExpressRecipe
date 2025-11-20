using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Interpreter
{
    public abstract class VariableBase
    {
        public virtual object GetValue()
        {
            return null;
        }
        public virtual void SetValue(object value)
        {
        }
    }

    public abstract class VariableBase<T> : VariableBase
    {
        protected T CurrentValue { get; set; }

        public override object GetValue()
        {
            return (T)CurrentValue;
        }
        public override void SetValue(object value)
        {
            CurrentValue = (T)value;
        }
    }
}
