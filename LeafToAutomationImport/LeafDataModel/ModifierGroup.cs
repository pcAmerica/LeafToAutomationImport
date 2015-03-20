using System.Collections.Generic;

namespace LeafToAutomationImport.LeafDataModel
{
    public class ModifierGroup
    {
        public string id { get; set; }
        public string groupName { get; set; }
        public string groupDesc { get; set; }

        public List<ModifierGroupSubItem> modifier_group_sub_items { get; set; }
        public List<ModifierGroupRule> modifier_group_rule { get; set; }
    }
}