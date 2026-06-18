using Dapper;
using Microsoft.Extensions.Logging;
using System.Data;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;

//using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v4._1;
using Teleperformance.DataIngestion.Models.Models.v4._1;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v4._1
{
    public class LandingLayerRepository : ILandingLayerRepository
    {
        private readonly ILogger<LandingLayerRepository> _logger;
        private readonly IDapperService _dapperService;
        ///private readonly ILandingLayerRepository _iLandingLayerRepository;
        public LandingLayerRepository(ILogger<LandingLayerRepository> logger,IDapperService dapperService)
        {
             _logger = logger;
            _dapperService = dapperService;
            //_iLandingLayerRepository = iLandingLayerRepository;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="message"></param>
        /// <param name="details"></param>
        /// <param name="moduleTypeId"></param>
        /// <param name="flpConfigurationId"></param>
        /// <param name="uploadFileId"></param>
        /// <returns></returns>

        public async Task<DatabaseResponse> AddActivityLog(string source,string message,string details,int moduleTypeId,string flpConfigurationId,string uploadFileId)
        {
            try
            {                
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@EIBId", null);
                dynamicParameters.Add("@source", source);
                dynamicParameters.Add("@message", message);
                dynamicParameters.Add("@details", details);
                dynamicParameters.Add("@moduleTypeId", moduleTypeId);
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@uplaodFileId", uploadFileId);
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_activityLog]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="flpConfigurationId"></param>
        /// <returns></returns>
        public async Task<FlpValidationDetails?> GetValidationDetailsAsync(string flpConfigurationId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);              
                using var con = _dapperService.CreateConnection();
                var dbResponse = await con.QueryMultipleAsync("[sel_flpConfigLandingLayerValidationDetails]", dynamicParameters, commandType: CommandType.StoredProcedure);
                var FileAdditionalDetails = dbResponse.Read<FileAdditionalDetails>()?.FirstOrDefault();
                var regexList = dbResponse.Read<FlpRegex>()?.ToList();
                var extensionList = dbResponse.Read<FlpExtensions>()?.ToList();
                var fileConfig = new FlpValidationDetails();
                if (fileConfig != null)
                {
                    fileConfig.FileAdditionalDetails = FileAdditionalDetails;
                    fileConfig.RegexList = regexList;
                    fileConfig.ExtensionList = extensionList;
                }
                return fileConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }


        public async Task<LandingLayerDetails?> GetLandingLayerDetailsAsync(string flpConfigurationId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                using var con = _dapperService.CreateConnection();
               // var dbResponse = await con.QueryFirstOrDefaultAsync("[sel_flpLandingLayerConfigurationByConfigId]", dynamicParameters, commandType: CommandType.StoredProcedure);
                var result = await _dapperService.GetSingleRowAsync<LandingLayerDetails>("[sel_flpLandingLayerConfigurationByConfigId]", dynamicParameters, commandType: CommandType.StoredProcedure);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="flpConfigurationId"></param>
        /// <param name="uploadFileId"></param>
        /// <param name="fileName"></param>
        /// <param name="changedFileName"></param>
        /// <param name="successFile"></param>
        /// <param name="fileURL"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<DatabaseResponse> AddLandingLayerFileDetails(string flpConfigurationId, string uploadFileId ,string fileName,string changedFileName,bool successFile, string fileURL,string message,bool landingLayerFolder)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@uploadFileId", uploadFileId);
                dynamicParameters.Add("@fileName", fileName);
                dynamicParameters.Add("@updatedFileName", changedFileName);
                dynamicParameters.Add("@successFile", successFile);
                dynamicParameters.Add("@fileURL", fileURL);
                dynamicParameters.Add("@message", message);
                dynamicParameters.Add("@movedLandingLayerFolder", landingLayerFolder);
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_uploadedLandingLayerFile]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }




    }
}
