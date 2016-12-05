using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Commands
{
    public abstract class VariableCommandBase<T> : ResultCommandBase<T>, IVariableCommand
        where T: VariableCommandBase<T>, new()
    {
        protected string Name { get; set; }
        protected string VariableType { get; set; }

        public VariableCommandBase()
        {
        }

        public override void Execute()
        {
            throw new NotImplementedException();
        }

        public abstract void Create(dynamic value);

        public abstract void Set(dynamic value);
        public abstract dynamic Get();

        public bool Exists()
        {
            return Interpreter.HasVariable(Name);
        }

        public void Create(string type, dynamic value)
        {
            VariableType = type;
            Create(value);
        }

        public override dynamic Result {
            get { return Get(); }
        }

        public override string ResultType {
            get { return VariableType; }
        }
    }
}
