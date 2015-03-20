using System.Collections.Generic;

namespace LeafToAutomationImport.LeafDataModel
{
    public class Store
    {
        public string siteName { get; set; }
        public string sitePhone { get; set; }
        public decimal foodTax { get; set; }
        public decimal bevTax { get; set; }
        public string siteEmail { get; set; }
        public decimal signatureLimit { get; set; }
        public decimal salesTax { get; set; }

        public Address primary_address { get; set; }
        public Catalog catalog { get; set; }
        public List<JobCode> job_codes { get; set; }
        public List<SitePayMethod> site_pay_methods { get; set; }
        public List<User> users { get; set; }
        public List<Printer> printers { get; set; }
    }
}