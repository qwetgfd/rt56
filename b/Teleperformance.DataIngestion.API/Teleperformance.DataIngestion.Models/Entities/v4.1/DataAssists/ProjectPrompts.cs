using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v4._1.DataAssists
{
    public class ProjectPrompts
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string FlowName { get; set; }
        public string Prompt { get; set; }
        public int flowId { get; set; }
        public string ProjectName { get; set; }
        public int Userid { get; set; }
    }
}
