using CodeGenerator.Core.Templates.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using CodeGenerator.Core.Templates.Parser;

namespace CodeGenerator.Core.Templates.Commands
{
    public class IntegerVariableDeclarationCommand : VariableDeclarationCommand<IntegerVariableDeclarationCommand>
    {
        public override void Execute()
        {
            Interpreter.SetVariable(VariableParameter.GetResult(), default(int));
        }

        public override CommandParserBase GetParser()
        {
            return new PrefixParser(this, "int ");
        }
    }
}
