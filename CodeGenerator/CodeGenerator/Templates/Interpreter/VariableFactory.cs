using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Interpreter
{
    public static class VariableFactory
    {
        public static VariableBase CreateInstance(string type)
        {
            if (string.Equals(type, "int", StringComparison.OrdinalIgnoreCase))
            {
                return new IntVariable();
            }
            else
            {
                if (string.Equals(type, "string", StringComparison.OrdinalIgnoreCase))
                {
                    return new StringVariable();
                }
                else
                {
                    if (string.Equals(type, "boolean", StringComparison.OrdinalIgnoreCase))
                    {
                        return new BooleanVariable();
                    }
                }
            }
            throw new Exception("Variable type " + type + " not found");
        }
    }
}
