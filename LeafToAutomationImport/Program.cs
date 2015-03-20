using System;
using LeafToAutomationImport.LeafDataModel;

namespace LeafToAutomationImport
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            using (var reader = System.IO.File.OpenText("site_export_sample.json"))
            {
                var store = ServiceStack.Text.JsonSerializer.DeserializeFromReader<Store>(reader);
                Console.WriteLine(store);
            }
        }
    }
}
