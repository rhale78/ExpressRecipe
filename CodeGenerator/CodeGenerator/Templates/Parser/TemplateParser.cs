using CodeGenerator.Core.Templates.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Parser
{
    public static class TemplateParser
    {
        public static TemplateLine CurrentTemplateLine { get; private set; }
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
            }
            while (CurrentTemplateLineIndex > -1);
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
}
