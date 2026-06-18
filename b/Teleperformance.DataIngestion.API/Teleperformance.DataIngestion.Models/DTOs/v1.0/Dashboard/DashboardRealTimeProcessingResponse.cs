using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard
{
    public class DashboardRealTimeProcessingResponse
    {
        public string ConfigurationId {  get; set; }
        public string processName {  get; set; }
        public string uploadFileId { get; set; }
        public string fileName {  get; set; }
        public int processStatusId {  get; set; }
        public DateTime creationDateTime { get; set; }
        public string processStatusName { get; set;}
    }
}
