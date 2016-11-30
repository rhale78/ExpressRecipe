using CodeGenerator.Core.Templates.Commands;
using CodeGenerator.Core.Templates.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.Templates.Parser
{
    public abstract class CommandParserBase
    {
        public CommandParserBase(TemplateParser templateParser)
        {
            TemplateParser = templateParser;
        }
        protected TemplateParser TemplateParser { get; set; }

        public abstract bool CanParse(string line);
        public abstract List<CommandParameterBase> Parse(string line);
    }

    public class TemplateParser
    {
        protected static TemplateLine CurrentTemplateLine { get; private set; }
        private static int CurrentTemplateLineIndex { get; set; }
        private static TemplateBase Template { get; set; }
        public static List<CommandBase> Parse(TemplateBase template)
        {
            Template = template;
            List<CommandBase> returnValue = new List<CommandBase>();
            do
            {


                //NeedsAdvance = false; //RSH - 11/29/16 - to handle block advancing last/final line of it - do we need to reset needs advance to prevent >1 line processed
                AdvanceTemplateLine();

            } while (CurrentTemplateLineIndex > -1);
            return returnValue;
        }

        static TemplateParser()
        {
            CurrentTemplateLineIndex = -1;
        }

        internal static void AdvanceTemplateLine()
        {
            CurrentTemplateLineIndex++;
            if (Template.Lines.Count > CurrentTemplateLineIndex)
            {
                CurrentTemplateLine = Template.Lines[CurrentTemplateLineIndex];
            }
            else
            {
                CurrentTemplateLine = null;
                CurrentTemplateLineIndex = -1;
            }
        }

    }

    public abstract class LineParserBase : CommandParserBase
    {
        public LineParserBase(TemplateParser templateParser) : base(templateParser)
        {
        }
    }

    public class PrefixParser : CommandParserBase
    {
        public PrefixParser(TemplateParser templateParser) : base(templateParser)
        {
        }

        public string Prefix { get; protected set; }

        public override bool CanParse(string line)
        {
            throw new NotImplementedException();
        }

        public override List<CommandParameterBase> Parse(string line)
        {
            throw new NotImplementedException();
        }
    }
    public class InfixParser : CommandParserBase
    {
        public InfixParser(TemplateParser templateParser) : base(templateParser)
        {
        }

        public string Infix { get; protected set; }

        public override bool CanParse(string line)
        {
            throw new NotImplementedException();
        }

        public override List<CommandParameterBase> Parse(string line)
        {
            throw new NotImplementedException();
        }
    }

    public class SurroundParser : CommandParserBase
    {
        public SurroundParser(TemplateParser templateParser) : base(templateParser)
        {
        }

        public string Prefix { get; protected set; }
        public string Postfix { get; protected set; }

        public override bool CanParse(string line)
        {
            throw new NotImplementedException();
        }

        public override List<CommandParameterBase> Parse(string line)
        {
            throw new NotImplementedException();
        }
    }

    public class BlockParser : CommandParserBase
    {
        public BlockParser(TemplateParser templateParser) : base(templateParser)
        {
        }

        public override bool CanParse(string line)
        {
            throw new NotImplementedException();
        }

        public override List<CommandParameterBase> Parse(string line)
        {
            throw new NotImplementedException();
        }
    }

}
