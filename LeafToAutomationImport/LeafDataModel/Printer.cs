using System;

namespace LeafToAutomationImport.LeafDataModel
{
    public class Printer
    {
        public string id { get; set; }
        public string printerName { get; set; }
        public string printerDesc { get; set; }
        public string ipAddress { get; set; }
        public DateTime? whenDeleted { get; set; }
        public string print_purpose { get; set; }
        public string printer_type { get; set; }
    }
}