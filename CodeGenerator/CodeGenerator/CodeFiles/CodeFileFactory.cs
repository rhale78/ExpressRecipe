using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.CodeFiles
{
    public class CodeFileFactory
    {
        private static Dictionary<string, CodeFileBase> FactoryTypes;

        static CodeFileFactory()
        {
            FactoryTypes = new Dictionary<string, CodeFileBase>(StringComparer.OrdinalIgnoreCase);
            FactoryTypes.Add("SQL", new SQLCodeFile());
            FactoryTypes.Add("CSharp", new CSharpCodeFile());
        }
        
        public static List<string> CodeFileTypes
        {
            get
            {
                return FactoryTypes.Keys.ToList();
            }
        }

        public static CodeFileBase Create(string type)
        {
            if (FactoryTypes.ContainsKey(type))
            {
                return FactoryTypes[type].CreateInstance();
            }
            else
            {
                throw new Exception("Code file type " + type + " not found");
            }
        }
    }
}
