using CodeGenerator.Core.CodeFiles;
using CodeGenerator.Core.Templates.Commands.Parameters;
using CodeGenerator.Core.Templates.Interpreter;
using CodeGenerator.Core.Templates.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.Templates.Commands
{
    public abstract class CommandBase
    {
        protected CommandInterpreter Interpreter { get; set; }

        protected CodeFileBase CodeFile
        {
            get
            {
                return Interpreter.CodeFile;
            }
        }

        protected CommandParserBase CommandParser { get; set; }

        public bool CanParse(string line)
        {
            return CommandParser.CanParse(line);
        }

        public void Parse(string line)
        {
            List<CommandParameterBase> commandParameters = CommandParser.Parse(line);
            ValidateParameters(commandParameters);
            AssignParameters(commandParameters);
        }

        protected void ValidateParameters(List<CommandParameterBase> commandParameters)
        {
            int index = 0;
            foreach (CommandParameterBase parameter in ValidParameterTypes)
            {
                if (commandParameters[index] is typeof(parameter))
                {

                }
                else
                {
                    throw new Exception("Type mismatch");
                }
            }
        }

        protected abstract void AssignParameters(List<CommandParameterBase> commandParameters);

        protected abstract List<CommandParameterBase> ValidParameterTypes { get; }

        public abstract void Execute();
    }

    public abstract class CommandBase<T> : CommandBase where T : CommandBase, new()
    {
        public T CreateInstance()
        {
            return new T();
        }
    }

    public class AssignmentCommand : CommandBase<AssignmentCommand>
    {
        protected IVariableCommand LHS { get; set; }
        protected IResultCommand RHS { get; set; }

        public AssignmentCommand()
        { }

        protected override List<CommandParameterBase> ValidParameterTypes
        {
            get
            {
                return new List<CommandParameterBase>() { typeof(IVariableCommand), typeof(IResultCommand) };
            }
        }

        public override void Execute()
        {
            if (LHS.Exists())
            {
                LHS.Set(RHS.Result);
            }
            else
            {
                LHS.Create(RHS.ResultType, RHS.Result);
            }
        }

        protected override void AssignParameters(List<CommandParameterBase> commandParameters)
        {
            LHS = (IVariableCommand)commandParameters[0];
            RHS = (IResultCommand)commandParameters[1];
        }
    }

    public interface IVariableCommand
    {
        bool Exists();
        void Create(string type, dynamic value);
        dynamic Get();
        void Set(dynamic value);
    }

    public interface IResultCommand
    {
        string ResultType { get; }
        dynamic Result { get; }
    }

    public abstract class ResultCommandBase<T> : CommandBase<T>, IResultCommand where T : ResultCommandBase<T>, new()
    {
        public abstract dynamic Result { get; }
        public abstract string ResultType { get; }
    }

    public abstract class VariableCommandBase<T> : ResultCommandBase<T>, IVariableCommand where T : VariableCommandBase<T>, new()
    {
        protected string Name { get; set; }
        protected string VariableType { get; set; }

        public VariableCommandBase()
        { }

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

        public override dynamic Result
        {
            get
            {
                return Get();
            }
        }

        public override string ResultType
        {
            get
            {
                return VariableType;
            }
        }

    }

    public class VariableCommand : VariableCommandBase<VariableCommand>
    {
        protected override List<CommandParameterBase> ValidParameterTypes
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public VariableCommand()
        { }

        public override void Create(dynamic value)
        {
            Interpreter.SetVariable(Name, VariableType, value);
        }

        public override void Set(dynamic value)
        {
            Interpreter.SetVariable(Name, value);
        }

        public override dynamic Get()
        {
            return Interpreter.GetVariableValue(Name);
        }

        protected override void AssignParameters(List<CommandParameterBase> commandParameters)
        {
            throw new NotImplementedException();
        }
    }

    public class GlobalVariableCommand : VariableCommandBase<GlobalVariableCommand>
    {
        protected override List<CommandParameterBase> ValidParameterTypes
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public GlobalVariableCommand()
        { }

        public override void Create(dynamic value)
        {
            Interpreter.SetGlobalVariable(Name, VariableType, value);
        }

        public override void Set(dynamic value)
        {
            Interpreter.SetGlobalVariable(Name, value);
        }

        public override dynamic Get()
        {
            return Interpreter.GetGlobalVariableValue(Name);
        }

        protected override void AssignParameters(List<CommandParameterBase> commandParameters)
        {
            throw new NotImplementedException();
        }
    }
}