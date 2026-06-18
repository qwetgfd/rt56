using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess
{
    public class SharedLocationDestinationServerDtoV4_1
    {
        public string ServerName { get; set; }
        public string FolderName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
    }
}
