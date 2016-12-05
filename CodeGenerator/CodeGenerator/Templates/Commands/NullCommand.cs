using CodeGenerator.Core.Templates.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using CodeGenerator.Core.Templates.Parser;

namespace CodeGenerator.Core.Templates.Commands
{
    public class NullCommand : CommandBase<NullCommand>
    {
        public override bool CanParseInternal(string[] parts)
        {
            return true;
        }

        public override void Execute()
        {
        }

        public override CommandParserBase GetParser()
        {
            return new NullParser(this);
        }

        public override List<CommandParameterBase> ParseInternal(string[] parts)
        {
            return new List<CommandParameterBase>();
        }

        protected override void AssignParameters(List<CommandParameterBase> commandParameters)
        {
        }
    }
}
