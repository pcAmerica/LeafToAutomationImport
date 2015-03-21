namespace LeafToAutomationImport.LeafDataModel
{
    public class JobCode
    {
        public string id { get; set; }
        public string jobCode { get; set; }
        public string jobCodeDesc { get; set; }
        public decimal rate1 { get; set; }
        public decimal rate2 { get; set; }
        public decimal otRate1 { get; set; }
        public decimal otRate2 { get; set; }
        public int hrsBeforeOT { get; set; }
    }
}