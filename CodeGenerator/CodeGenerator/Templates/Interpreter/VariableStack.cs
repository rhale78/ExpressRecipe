using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Interpreter
{
    public class VariableStack
    {
        protected Stack<VariableStackFrame> VariableStacks { get; set; }
        protected VariableStackFrame GlobalVariables { get; set; }

        public VariableStack()
        {
            VariableStacks = new Stack<VariableStackFrame>();
            VariableStacks.Push(new VariableStackFrame());
            GlobalVariables = VariableStacks.Peek();
        }

        protected VariableStackFrame CurrentStack {
            get { return VariableStacks.Peek(); }
        }

        public void CreateStackFrame()
        {
            VariableStacks.Push(new VariableStackFrame());
        }
        public void DestroyStackFrame()
        {
            VariableStacks.Pop();
        }

        public void SetGlobalVariable(string name, string type, dynamic value)
        {
            GlobalVariables.SetVariable(name, type, value);
        }
        public void SetGlobalVariable(string name, dynamic value)
        {
            GlobalVariables.SetVariable(name, value);
        }
        public VariableBase GetGlobalVariable(string name)
        {
            return GlobalVariables.GetVariable(name);
        }
        public dynamic GetGlobalVariableValue(string name)
        {
            return GlobalVariables.GetVariableValue(name);
        }

        public void SetVariable(string name, string type, dynamic value)
        {
            CurrentStack.SetVariable(name, type, value);
        }
        public void SetVariable(string name, dynamic value)
        {
            CurrentStack.SetVariable(name, value);
        }
        public VariableBase GetVariable(string name)
        {
            return CurrentStack.GetVariable(name);
        }
        public dynamic GetVariableValue(string name)
        {
            return CurrentStack.GetVariableValue(name);
        }

        internal bool HasVariable(string name)
        {
            bool returnValue = GlobalVariables.HasVariable(name);
            if (!returnValue)
            {
                returnValue = CurrentStack.HasVariable(name);
            }
            return returnValue;
        }
    }
}
