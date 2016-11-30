using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.Templates.Interpreter
{
    public abstract class VariableBase
    {
        public virtual object GetValue() { return null; }
        public virtual void SetValue(object value) { }
    }

    public abstract class VariableBase<T>:VariableBase
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

    public static class VariableFactory
    {
        public static VariableBase CreateInstance(string type)
        {
            if (string.Equals(type, "int",StringComparison.OrdinalIgnoreCase))
            {
                return new IntVariable();
            }
            else if (string.Equals(type, "string", StringComparison.OrdinalIgnoreCase))
            {
                return new StringVariable();
            }
            else if (string.Equals(type, "boolean", StringComparison.OrdinalIgnoreCase))
            {
                return new BooleanVariable();
            }
            throw new Exception("Variable type " + type + " not found");
        }
    }

    public class IntVariable : VariableBase<int>
    { }
    public class StringVariable:VariableBase<string>
    { }
    public class BooleanVariable : VariableBase<bool>
    { }

}
