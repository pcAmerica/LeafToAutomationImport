using System.Collections.Generic;

namespace LeafToAutomationImport.LeafDataModel
{
    public class Item
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public decimal price { get; set; }
        public bool isFood { get; set; }
        public bool isTaxable { get; set; }
        public int position { get; set; }
        public string barcode { get; set; }
        public string unitType { get; set; }
        public decimal cost { get; set; }
        public string vendorSKU { get; set; }
        public string ventorItemNum { get; set; }
        public string merchant_code { get; set; }
        public string category_id { get; set; }
        public string printer_id { get; set; }
        public string tax_type { get; set; }

        public List<ModifierItemGroup> modifier_item_groups { get; set; }
    }
}