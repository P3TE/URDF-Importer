using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Unity.Robotics.UrdfImporter
{
    public class PluginReflectionHelper
    {
        
        public static List<FieldInfo> GetConstants(Type type)
        {
            FieldInfo[] fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            return fieldInfos.Where(fi => fi.IsLiteral && !fi.IsInitOnly).ToList();
        }

        public static List<Tuple<string, string>> GetAllStringConstants(Type type)
        {
            List<FieldInfo> fieldInfos = GetConstants(type);
            List<Tuple<string, string>> result = new List<Tuple<string, string>>();
            foreach (FieldInfo fieldInfo in fieldInfos)
            {
                if (fieldInfo.FieldType == typeof(string))
                {
                    result.Add(new Tuple<string, string>(fieldInfo.Name, (string) fieldInfo.GetRawConstantValue()));
                }
            }

            return result;
        }
        
        /**
         * Takes a static class in the form of:
         * static class ListOfIds{
         *  public const string IDA = "id_a";
         *  public const string IDB = "id_b";
         * }
         * and generates a HashSet<string> with the values ("id_a", "id_b")
         */
        public static HashSet<string> GetConstStringValues(Type staticClassWithIdsType)
        {
            List<Tuple<string, string>> constStrings = PluginReflectionHelper.GetAllStringConstants(staticClassWithIdsType);
            HashSet<string> result = new HashSet<string>();
            foreach (Tuple<string, string> keyValuePair in constStrings)
            {
                result.Add(keyValuePair.Item2);
            }
            return result;
        }
        
        
    }
}