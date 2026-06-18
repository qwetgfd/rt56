using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingProcess
{
    public class FileProcessLogHistoryDto
    {
        public long processId { get; set; }
        public string tableName { get; set; }
        public string messageType { get; set; }
        public string message { get; set; }
        public int totalRows { get; set; }
        public string loginid { get; set; }
        public DateTime dateTimeReceived { get; set; }
        public string processName { get; set; }
        public string fileType { get; set; }
        public string flpConfigurationId { get; set; }
        public int processTypeId { get; set; }
        public int flpFileLogStatusId { get; set; }
        public int activityProcessStatusId { get; set; }
        public string fileUploadedId { get; set; }
        public string databricksAPIResponse { get; set; }
        public string tabName { get; set; }
    }
}
