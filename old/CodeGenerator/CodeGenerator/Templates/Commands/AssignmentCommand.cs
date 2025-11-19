using CodeGenerator.Core.Templates.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using CodeGenerator.Core.Templates.Parser;

namespace CodeGenerator.Core.Templates.Commands
{
    public class DoubleVariableDeclarationCommand : VariableDeclarationCommand<DoubleVariableDeclarationCommand>
    {
        public override void Execute()
        {
            Interpreter.SetVariable(VariableParameter.GetResult(), default(double));
        }

        public override CommandParserBase GetParser()
        {
            return new PrefixParser(this, "double ");
        }
    }
    public class StringVariableDeclarationCommand : VariableDeclarationCommand<StringVariableDeclarationCommand>
    {
        public override void Execute()
        {
            Interpreter.SetVariable(VariableParameter.GetResult(), null);
        }

        public override CommandParserBase GetParser()
        {
            return new PrefixParser(this, "string ");
        }
    }

    public class AssignmentCommand : CommandBase<AssignmentCommand>
    {
        protected IVariableCommand LHS { get; set; }
        protected IResultCommand RHS { get; set; }

        public AssignmentCommand()
        {
        }

        //protected override List<CommandParameterBase> ValidParameterTypes {
        //    get { return new List<CommandParameterBase>()
        //        { typeof(IVariableCommand), typeof(IResultCommand)
        //        }; }
        //}

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

        public override bool CanParseInternal(string[] parts)
        {
            throw new NotImplementedException();
        }

        public override List<CommandParameterBase> ParseInternal(string[] parts)
        {
            throw new NotImplementedException();
        }

        public override CommandParserBase GetParser()
        {
            return new InfixParser(this, "=");
        }
    }
}
