using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v4._1.EIB
{
    public class PDMProflingSPNames
    {
        public int id { get; set; }
        public string sPName { get; set; }
        public string description { get; set; }
        public string LatestStatus { get; set; }
        public string createdBy { get; set; }
        public string insertedAt { get; set; }
    }
}
