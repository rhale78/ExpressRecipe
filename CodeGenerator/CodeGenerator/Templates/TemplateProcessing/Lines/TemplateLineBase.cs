using CodeGenerator.Core.Templates.Interpreter;

namespace CodeGenerator.Core.Templates
{
	public abstract class TemplateLineBase
	{
		internal TemplateLineBase(string line)
		{
			Line = line;
		}

		public string Line { get; set; }
		public CommandInterpreter Interpreter { get; set; }

		public abstract string Result
		{
			get;
		}
	}
}
