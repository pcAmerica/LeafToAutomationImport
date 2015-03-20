using System.Collections.Generic;

namespace LeafToAutomationImport.LeafDataModel
{
    public class User
    {
        public string id { get; set; }
        public string userName { get; set; }
        public string first { get; set; }
        public string last { get; set; }
        public string email { get; set; }
        public string phone { get; set; }
        public bool deleted { get; set; }
        public List<JobCodeUser> job_code_users { get; set; }
    }
}