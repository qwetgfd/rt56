using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration
{
    public class FlpFileColumnMapping
    {
        public string flpConfigurationId { get; set; }
        public string fileColumn { get; set; }
        public string dbColumn { get; set; }
        public int dataTypeId { get; set; }
        public int formatId { get; set; }
        public string tabName { get; set; }
        public string dataType { get; set; }
    }
}
