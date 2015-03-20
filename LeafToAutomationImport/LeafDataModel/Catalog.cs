using System.Collections.Generic;

namespace LeafToAutomationImport.LeafDataModel
{
    public class Catalog
    {
        public List<Item> items { get; set; }
        public List<Modifier> modifiers { get; set; }
        public List<ModifierGroup> modifier_groups { get; set; }
        public List<Category> categories { get; set; }
    }
}