using Dapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Account;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingProcess.TableSchemaValidation;
using Teleperformance.DataIngestion.Models.Entities.v2._0.EmailNotification;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v2._0
{
    public class EmailNotificationRepository: IEmailNotificationRepository
    {      
        private readonly ILogger<EmailNotificationRepository> _logger;
        private readonly IDapperService _dapperService;

        public EmailNotificationRepository(ILogger<EmailNotificationRepository> logger, IDapperService dapperService)
        {          
            _logger = logger;
            _dapperService = dapperService;
        }


        //public async Task<DatabaseResponse?> CommitEmailNotification(string flpConfigurationId, string uploadFileId)
        //{

        //    try
        //    {
        //        var parameters = new DynamicParameters();
        //        parameters.Add("@uploadFileId", uploadFileId);
        //        parameters.Add("@flpConfigurationId", flpConfigurationId);
        //        return await _dapperService.GetSingleRowAsync<DatabaseResponse>("[dbo].[commit_emailNotfication]",
        //                    parameters, commandType: CommandType.StoredProcedure);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, ex.Message);
        //        throw;
        //    }
        //}


        public async Task<IEnumerable<EmailNotification>> GetEmailNotificationList()
        {
            try
            {
                              
                var dbResponse = await _dapperService.GetMultipleRowsAsync<EmailNotification>("[sel_flpSendEmailNotification]", null, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString());
                throw;
            }

        }


        public async Task<EmailNotification> GetEmailNotificationDetailByIds(string flpConfigurationId,string uploadFileId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@uploadFileId", uploadFileId);
                var dbResponse = await _dapperService.GetSingleRowAsync<EmailNotification>("[sel_flpSendEmailNotificationByIds]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString());
                throw;
            }

        }


        public async Task<IEnumerable<EmailNotification>> GetMultisheetEmailNotifications(string flpConfigurationId, string uploadFileId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@uploadFileId", uploadFileId);
                var dbResponse = await _dapperService.GetMultipleRowsAsync<EmailNotification>("[sel_flpSendEmailNotificationByIds]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString());
                throw;
            }
        }

        public async Task<EmailNotificationTemplate> GetEmailNotificationTemplate()
        {
            try
            {

                var dbResponse = await _dapperService.GetSingleRowAsync<EmailNotificationTemplate>("[sel_emailNotificationTemplate]", null, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString());
                throw;
            }

        }

        public async Task<DatabaseResponse> CommitEmailNotification(string flpConfigurationId,string uploadFileId,bool isEmailSent,string tabName)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@uploadFileId", uploadFileId);
                dynamicParameters.Add("@isEmailSent", isEmailSent);
                if(!string.IsNullOrWhiteSpace(tabName))
                    dynamicParameters.Add("@tabName", tabName);

                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_emailNotification]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString());
                throw;
            }

        }

    }
}
