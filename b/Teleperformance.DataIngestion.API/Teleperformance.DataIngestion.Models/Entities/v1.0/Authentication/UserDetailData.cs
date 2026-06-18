using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.Authentication
{
    public class AuthCredentials
    {
        public string NTID { get; set; }
    }
    public class UserDetailData
    {
        public string token { get; set; }
        public UserResponseDetails userDetail { get; set; }
    }
    public class UserResponseDetails
    {
        public int CaseId { get; set; }
        public string ccmsId { get; set; }
        public string JobTitle { get; set; }
        public DateTime DateHired { get; set; }
        public string userName { get; set; }
        public string tpusaFlag { get; set; }
        public string program { get; set; }

        public bool IsAdmin { get; set; }

    }
}
