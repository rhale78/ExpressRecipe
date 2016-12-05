using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Commands.Parameters
{
    public static class LiteralCommandParameterFactory
    {
        private static Dictionary<string, LiteralCommandParameterBase> ParameterTypes;
        static LiteralCommandParameterFactory()
        {
            ParameterTypes = new Dictionary<string, LiteralCommandParameterBase>(StringComparer.OrdinalIgnoreCase);
            ParameterTypes.Add("int", new IntLiteralCommandParameter());
            ParameterTypes.Add("bool", new BoolLiteralCommandParameter());
            ParameterTypes.Add("string", new StringLiteralCommandParameter());
            ParameterTypes.Add("double", new DoubleLiteralCommandParameter());
        }

        public static LiteralCommandParameterBase GetLiteralParameter(string type, dynamic value, bool throwExceptions = true)
        {
            if (ParameterTypes.ContainsKey(type))
            {
                return ParameterTypes[type].CreateInstance(value);
            }
            else
            {
                if (throwExceptions)
                {
                    throw new ArgumentException("Type " + type + " is unknown as a literal type");
                }
            }
            return new VariableNameCommandParameter(value);
        }
    }
}
