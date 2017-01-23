namespace CodeGenerator.Core.Templates
{

	public class StaticTemplateLine : TemplateLineBase
	{
		internal StaticTemplateLine(string line)
			: base(line)
		{
		}

		public override string Result
		{
			get { return Line+System.Environment.NewLine; }
		}

		public override string ToString()
		{
			return Result;
		}
	}
}
