using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.EIB
{
    public class DBViewsResponseDto
    {
        public int viewId { get; set; }
        public string viewName { get; set; }
        public int columnCount { get; set; }
    }

    public class DBSPsResponseDto
    {
        public int id { get; set; }
        public string runId { get; set; }
        public string sPName { get; set; }
        public string description { get; set; }
        public string LatestStatus { get; set; }
        public string createdBy { get; set; }
        public string insertedAt { get; set; }
        public List<ProflingRunHistoryLog>? profilingRunHistoryLog { get; set; }
    }

    //public string EIBId { get; set; }
    //public string EIBName { get; set; }
    //public string description { get; set; }
    //public string createdBy { get; set; }
    //public string creationDateTime { get; set; }
    //public string modifiedDateTime { get; set; }
    //public string updatedBy { get; set; }
    //public string mappedCount { get; set; }
    //public string status { get; set; }

    public class RegisterPDMRProfilingSPRunResponseDto : DatabaseResponse
    {
    }

    public class EIBGenerationStatusDto
    {
        public string EIBId { get; set; }
        public string status { get; set; }
        public string fileURL { get; set; }
        public bool hasActiveFileURL { get; set; }
        public string errorMessage { get; set; }
        public string generationStartDateTime { get; set; }
    }

    public class EIBCountryDto
    {
        public int id { get; set; }
        public string countryName { get; set; }
        public string description { get; set; }
    }

    public class ProfilingSPConfiguration
    {
        public int id { get; set; }
        public string runId { get; set; }
        public string spName { get; set; }
        public string status { get; set; }
        public string description { get; set; }
        public string databaseServer { get; set; }
        public string databaseName { get; set; }
        public string databaseConnectionSecret { get; set; }
        public string latestStatus { get; set; }

        public List<ProflingRunHistoryLog>? proflingRunHistoryLogs { get; set; } = new List<ProflingRunHistoryLog>();
    }

    public class ProflingRunHistoryLog
    {
        public string runId { get; set; }
        public string ProcessedBy { get; set; }
        public string StartedAt { get; set; }
        public string endedAt { get; set; }
        public string status { get; set; }
    }
}
