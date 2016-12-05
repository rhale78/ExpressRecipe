using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Interpreter
{
    public class VariableStackFrame
    {
        internal Dictionary<string, VariableBase> Variables { get; set; }

        public VariableStackFrame()
        {
            Variables = new Dictionary<string, VariableBase>();
        }

        public void SetVariable(string name, string type, dynamic value)
        {
            if (!Variables.ContainsKey(name))
            {
                VariableBase variable = VariableFactory.CreateInstance(type);
                Variables.Add(name, variable);
            }
            Variables[name].SetValue(value);
        }
        public void SetVariable(string name, dynamic value)
        {
            VariableBase variableBase = GetVariable(name);
            if (variableBase != null)
            {
                variableBase.SetValue(value);
            }
        }
        public dynamic GetVariableValue(string name)
        {
            VariableBase variableBase = GetVariable(name);
            if (variableBase != null)
            {
                return variableBase.GetValue();
            }
            return null;
        }
        public VariableBase GetVariable(string name)
        {
            if (Variables.ContainsKey(name))
            {
                return Variables[name];
            }
            return null;
        }

        internal bool HasVariable(string name)
        {
            return GetVariable(name) != null;
        }
    }
}
