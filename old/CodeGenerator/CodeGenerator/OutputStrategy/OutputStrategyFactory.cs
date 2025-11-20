using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.OutputStrategy
{
    public static class OutputStrategyFactory
    {
        private static Dictionary<string, OutputStrategyBase> FactoryTypes { get; set; }

        static OutputStrategyFactory()
        {
            FactoryTypes = new Dictionary<string, OutputStrategyBase>();
            FactoryTypes.Add("Console", new ConsoleOutputStrategy());
            FactoryTypes.Add("File Default", new FileDefaultStrategy());
            FactoryTypes.Add("File", new FileOutputStrategy());
            FactoryTypes.Add("String", new StringOutputStrategy());
        }

        public static List<string> StrategyTypes {
            get { return FactoryTypes.Keys.ToList(); }
        }

        public static OutputStrategyBase Strategy(string type)
        {
            if (FactoryTypes.ContainsKey(type))
            {
                return FactoryTypes[type];
            }
            else
            {
                throw new Exception("Output strategy type " + type + " not found");
            }
        }
    }
}
