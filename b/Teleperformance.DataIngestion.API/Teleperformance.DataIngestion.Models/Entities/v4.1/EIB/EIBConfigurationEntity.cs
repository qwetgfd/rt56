using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.EIB;

namespace Teleperformance.DataIngestion.Models.Entities.v4._1.EIB
{
    public class EIBConfigurationList
    {
        public List<EIBConfigurationEntity> Response { get; set; }
        public int? TotalCount { get; set; }
    }
    public class EIBConfigurationEntity
    {
        public string EIBId { get; set; }
        public string EIBName { get; set; }
        public string description { get; set; }
        public string createdBy { get; set; }
        public string creationDateTime { get; set; }
        public string? modifiedDateTime { get; set; }
        public string? updatedBy { get; set; }
        public string mappedCount { get; set; }
        public string status { get; set; }
        public string fileURL { get; set; }
        public string? generationStartDateTime { get; set; }
        public string? generationEndDateTime { get; set; }
    }


}
