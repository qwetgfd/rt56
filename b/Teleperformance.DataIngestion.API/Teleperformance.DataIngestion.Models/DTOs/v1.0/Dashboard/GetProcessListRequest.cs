using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard
{
    public class GetProcessListRequest:ClsSecurityGroupBase
    {
        public int RegionId { get;set;}
        public string? SubRegionId { get; set; }
        public int ClientId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int noOfRecords { get; set; }
    }
}
