using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.OverwriteStrategies
{
    public static class OverwriteStrategyFactory
    {
        private static Dictionary<string,OverwriteStrategyBase> FactoryTypes { get; set; }

        static OverwriteStrategyFactory()
        {
            FactoryTypes = new Dictionary<string, OverwriteStrategyBase>(StringComparer.OrdinalIgnoreCase);

            FactoryTypes.Add("File Default", new FileDefaultStrategy());
            FactoryTypes.Add("Always Overwrite", new AlwaysOverwriteStrategy());
            FactoryTypes.Add("Create Only", new CreateIfNotExistsStrategy());
        }

        public static List<string> StrategyTypes {
            get
            {
                return FactoryTypes.Keys.ToList();
            }
        }

        public static OverwriteStrategyBase Strategy(string type)
        {
            if(FactoryTypes.ContainsKey(type))
            {
                return FactoryTypes[type];
            }
            else
            {
                throw new Exception("Overwrite strategy type "+type + " not found");
            }
        }
    }
}
