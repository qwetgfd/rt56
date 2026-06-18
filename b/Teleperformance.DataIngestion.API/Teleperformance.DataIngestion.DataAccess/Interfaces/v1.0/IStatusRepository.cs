using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus;
using Teleperformance.DataIngestion.Models.Entities.v1._0.UploadFileStatus;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IStatusRepository
    {
        Task<IEnumerable<ConfigFileStatusResponse>> FlpFileStatus();
        Task<IEnumerable<ConfigFileStatusResponse>> StatusFlpconfigurationID(StatusRequest statusRequest);
        Task<IEnumerable<FileStatusReportResponse>> StatusUploadFileReport(FileUploadDetailedStatusRequest fileUploadDetailedStatusRequest);

        Task<ProcessedFileResponse> GetFlpProcessedFilesListAsync(ProcessedFileListRequestDto request);
        Task<ProcessedFileResponse> GetFlpProcessedFilesListAsyncV2(ProcessedFileListRequestDto request);
       
    }
}
