using AutoMapper;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v2._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v2._0.EmailNotification;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v2._0.EmailNotification;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Enums.v3._0;
using TPMailerService;

namespace Teleperformance.DataIngestion.DataAccess.Services.v2._0
{
    public class EmailNotificationService : IEmailNotificationService
    {
        private readonly IEmailNotificationRepository _emailNotificationRepository;
        private readonly ILogger<EmailNotificationService> _logger;
        private readonly ICache _cache;
        private readonly IBlobStorageService _iBlobStorageService;
        public EmailNotificationService(IEmailNotificationRepository emailNotificationRepository, ILogger<EmailNotificationService> logger, ICache cache, IBlobStorageService iBlobStorageService)
        {
            _emailNotificationRepository = emailNotificationRepository;
            _logger = logger;
            _cache = cache;
            _iBlobStorageService = iBlobStorageService;
        }


        public async Task<APIResponse<IEnumerable<EmailNotificationRequestDto>>> GetEmailNotificationList()
        {
            var emailNotifications = await _emailNotificationRepository.GetEmailNotificationList();

            if (emailNotifications == null)
            {
                _logger.LogInformation("No records found.");
                return await Task.FromResult(new APIResponse<IEnumerable<EmailNotificationRequestDto>>
                {
                    ResultStatus = APIResultStatus.NoContent,
                    ResponseMessage = new List<string> { "No records found." },
                    Result = null
                });
            }

            if (!emailNotifications.Any())
            {

                _logger.LogInformation("No records found in flpConfigurations.");
                return await Task.FromResult(new APIResponse<IEnumerable<EmailNotificationRequestDto>>
                {
                    ResultStatus = APIResultStatus.NoContent,
                    ResponseMessage = new List<string> { "No records found." },
                    Result = null
                });
            }
            if (emailNotifications.Any() && string.Compare(emailNotifications.FirstOrDefault()?.Result, "Failure", true) == 0)
            {
                _logger.LogError("Return failure from database.");
                return await Task.FromResult(new APIResponse<IEnumerable<EmailNotificationRequestDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Internal server error" },
                    Result = null
                });
            }

            var emailNotificationList = emailNotifications.Select(en => new EmailNotificationRequestDto
            {
                FlpConfigurationId = en.flpConfigurationId,
                UploadFileId = en.uploadFileId
            });
            

            //var flpConfigurationArray = await Task.WhenAll(flpConfigurationList);
            return new APIResponse<IEnumerable<EmailNotificationRequestDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = emailNotificationList
            };


        }


        public async Task<APIResponse<bool>> SendEmailNotification(EmailNotificationRequestDto emailNotificationDto)
        {
            bool isEmailSent = false;
            try
            {
               
                var dbResponse = await _emailNotificationRepository.GetEmailNotificationDetailByIds(emailNotificationDto.FlpConfigurationId, emailNotificationDto.UploadFileId);
                if (dbResponse == null)
                {
                    string message = $"Not found details for this {emailNotificationDto.FlpConfigurationId}";
                    _logger.LogError(message);
                    return new APIResponse<bool>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { message },
                        Result = false
                    };
                }
                if (string.Compare(dbResponse.Result, "Failure", true) == 0)
                {
                    string message = "Return failure from database. for flpConfigurationId " + emailNotificationDto.FlpConfigurationId;
                    _logger.LogError(message);
                    return new APIResponse<bool>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { message },
                        Result = false
                    };
                }

                if (string.IsNullOrWhiteSpace(dbResponse.emailAddress))
                {
                    string message = "emailAddress does not exist for flpConfigurationId " + emailNotificationDto.FlpConfigurationId;
                    _logger.LogError(message);
                    return new APIResponse<bool>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { message },
                        Result = false
                    };
                }


                if (string.IsNullOrWhiteSpace(dbResponse.blobName) && string.IsNullOrWhiteSpace(dbResponse.errorMsg))
                {
                    dbResponse.errorMsg = "Not found destination blob name. File not moved in blob";
                }



                if (dbResponse.multisheet)
                {
                    var singleSheetTempRes = await SendMultiSheetTemplate(emailNotificationDto);
                    return singleSheetTempRes;
                }
                else
                {
                    var emailNotification = new EmailNotificationDto()
                    {
                        FlpProcessStatusId = dbResponse.flpProcessStatusId,
                        FlpConfigurationId = dbResponse.flpConfigurationId,
                        UploadFileId = dbResponse.uploadFileId,
                        Error = dbResponse.errorMsg,
                        FileName = dbResponse.uploadFileName,
                        StartTime = dbResponse.startTime,
                        EndTime = dbResponse.endTime,
                        Client = dbResponse.clientName,
                        SubRegion = dbResponse.subRegion,
                        Region = dbResponse.region,
                        DuplicateRecords = dbResponse.duplicateRecords,
                        ProcessedRecords = dbResponse.processedRecords,
                        SendToEmail = dbResponse.emailAddress,
                        TotalDuration = dbResponse.durationInSeconds,
                        TotalRecords = dbResponse.totalRecords,
                        SourceStorageAccount = dbResponse.sourceStorageAccount,
                        sourceContainerName = dbResponse.sourceContainerName,
                        SourceStorageAccountKey = dbResponse.sourceStorageAccountKey,
                        BlobName = dbResponse.blobName,
                        Status = dbResponse.statusName,
                        Stage = dbResponse.stageName,
                        BackUpFileDetailsId = dbResponse.backUpFileDetailsId,
                        Description = dbResponse.Description,
                        FailureCount = dbResponse.failureCount,
                        SuccessCount = dbResponse.successCount,
                        TotalFileCount = dbResponse.totalFileCount,
                        FileProcessingServerTypeId = dbResponse.fileProcessingServerTypeId,
                        ConfigurationName = dbResponse.configurationName


                    };
                    
                    var singleSheetTempRes = await SendSingleSheetTemplate(emailNotification);
                    return singleSheetTempRes;
                }





            }
            catch (Exception ex)
            {
                // await _emailNotificationRepository.CommitEmailNotification(emailNotificationDto.FlpConfigurationId, emailNotificationDto.UploadFileId, isEmailSent);
                //throw new Exception(ex.Message.ToString());
                _logger.LogError($"Email Error: {ex.Message.ToString()}");

                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { $"Error occurred:{ex.Message.ToString()}" },
                    Result = false
                };
            }
        }









        public async Task<APIResponse<bool>> SendMultiSheetTemplate(EmailNotificationRequestDto emailNotificationDto)
        {
            bool isEmailSent = false;
            try
            {
                var dbResponse = await _emailNotificationRepository.GetMultisheetEmailNotifications(emailNotificationDto.FlpConfigurationId, emailNotificationDto.UploadFileId);
                if (dbResponse == null)
                {
                    string message = $"Not found details for this {emailNotificationDto.FlpConfigurationId}";
                    _logger.LogError(message);
                    return new APIResponse<bool>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { message },
                        Result = false
                    };
                }
                if (string.Compare(dbResponse.FirstOrDefault()?.Result, "Failure", true) == 0)
                {
                    string message = "Return failure from database. for flpConfigurationId " + emailNotificationDto.FlpConfigurationId;
                    _logger.LogError(message);
                    return new APIResponse<bool>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { message },
                        Result = false
                    };
                }

                if (string.IsNullOrWhiteSpace(dbResponse.FirstOrDefault()?.emailAddress))
                {
                    string message = "emailAddress does not exist for flpConfigurationId " + emailNotificationDto.FlpConfigurationId;
                    _logger.LogError(message);
                    return new APIResponse<bool>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { message },
                        Result = false
                    };
                }


                MultisheetEmailNotificationDto multisheetEmail = new MultisheetEmailNotificationDto();
                var res = dbResponse.FirstOrDefault();
                if (res != null)
                {
                    multisheetEmail.FlpConfigurationId = res.flpConfigurationId;
                    multisheetEmail.UploadFileId = res.uploadFileId;
                    // multisheetEmail.TabName = res.tabName;
                    multisheetEmail.Region = res.region;
                    multisheetEmail.SubRegion = res.subRegion;
                    multisheetEmail.ClientName = res.clientName;
                    multisheetEmail.Description = res.Description;
                    multisheetEmail.FileName = res.uploadFileName;
                    multisheetEmail.SentToEmail = res.emailAddress;
                    multisheetEmail.TabNames = new List<TabNameDto>();
                }

                foreach (var item in dbResponse)
                {
                    TabNameDto tabNameDto = new TabNameDto();
                    tabNameDto.TabName = item.tabName;
                    tabNameDto.SuccessProcess = item.successProcess;
                    //EmailNotificationDto emailNotification = null;

                    tabNameDto.NotificationDetail = new EmailNotificationDto()
                    {
                        FlpProcessStatusId = item.flpProcessStatusId,
                        FlpConfigurationId = item.flpConfigurationId,
                        UploadFileId = item.uploadFileId,
                        Error = (string.IsNullOrWhiteSpace(item?.blobName) && string.IsNullOrWhiteSpace(item?.errorMsg)) ? "Not found destination blob name. File not moved in blob" : item.errorMsg,
                        FileName = item.uploadFileName,
                        StartTime = item.startTime,
                        EndTime = item.endTime,
                        Client = item.clientName,
                        SubRegion = item.subRegion,
                        Region = item.region,
                        DuplicateRecords = item.duplicateRecords,
                        ProcessedRecords = item.processedRecords,
                        SendToEmail = item.emailAddress,
                        TotalDuration = item.durationInSeconds,
                        TotalRecords = item.totalRecords,
                        SourceStorageAccount = item.sourceStorageAccount,
                        sourceContainerName = item.sourceContainerName,
                        SourceStorageAccountKey = item.sourceStorageAccountKey,
                        BlobName = item.blobName,
                        Status = item.statusName,
                        Stage = item.stageName,
                        BackUpFileDetailsId = item.backUpFileDetailsId,
                        Description = item.Description,
                        SucessProcess = item.successProcess
                    };
                    multisheetEmail.TabNames.Add(tabNameDto);

                }


                if (multisheetEmail.TabNames.Any(x=>x.SuccessProcess))
                {
                    var emailNotificationDetails = await GetMultisheetEmailTemplateForSuccess(multisheetEmail);

                    if (emailNotificationDetails == null)
                    {
                        string message = $"Multisheet Error: Not sent  email for this {emailNotificationDto.FlpConfigurationId} and config due empty template details";
                        _logger.LogError(message);
                        return new APIResponse<bool>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { message },
                            Result = false
                        };
                    }
                    if (!emailNotificationDetails.SuccessEmailToBeSent)
                    {
                        string message = $"Multisheet Error: Sent Email is off at this moment for {multisheetEmail.FlpConfigurationId}";
                        _logger.LogError(message);
                        return new APIResponse<bool>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { message },
                            Result = false
                        };
                    }
                    isEmailSent = await EmailNotificationHelper.SendMail(emailNotificationDetails.MailBody, emailNotificationDetails.SendToEmail, emailNotificationDetails.SuccessSubject, emailNotificationDetails.FromAddress);

                    if (isEmailSent)
                    {
                        foreach (var tab in multisheetEmail.TabNames.Where(x => x.SuccessProcess))
                            await _emailNotificationRepository.CommitEmailNotification(emailNotificationDto.FlpConfigurationId, emailNotificationDto.UploadFileId, isEmailSent, tab.TabName);
                    }

                }
                
                
                if (multisheetEmail.TabNames.Any(x => !x.SuccessProcess))
                {
                    var blobName = dbResponse.FirstOrDefault()?.blobName;
                    var sourceStorageAccount = dbResponse.FirstOrDefault()?.sourceStorageAccount;
                    var sourceStorageAccountKey = dbResponse.FirstOrDefault()?.sourceStorageAccountKey;
                    var sourceContainerName = dbResponse.FirstOrDefault()?.sourceContainerName;
                    var emailNotificationDetails = await GetMultisheetEmailTemplateForFailure(multisheetEmail);

                    if (emailNotificationDetails == null)
                    {
                        string message = $"Multisheet Error: Not sent  email for this {emailNotificationDto.FlpConfigurationId} and config due empty template details";
                        _logger.LogError(message);
                        return new APIResponse<bool>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { message },
                            Result = false
                        };
                    }
                    if (!emailNotificationDetails.FailureEmailToBeSent)
                    {
                        string message = $"Multisheet Error: Sent Email is off at this moment for {multisheetEmail.FlpConfigurationId}";
                        _logger.LogError(message);
                        return new APIResponse<bool>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { message },
                            Result = false
                        };
                    }

                    if (!string.IsNullOrWhiteSpace(blobName))
                    {
                        string sourceBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(sourceStorageAccount, sourceStorageAccountKey);


                        BlobClient blobClient = _iBlobStorageService.GetBlobClientDetails(blobName, sourceBlobConnectionString, sourceContainerName);

                        string fileUrlText = EmailNotificationHelper.GetSasUri(blobClient);
                        if (!string.IsNullOrWhiteSpace(fileUrlText))
                        {
                            //var stringBuilder = new StringBuilder();
                            //stringBuilder.Append(emailNotificationDetails.MailBody);
                            //stringBuilder.Append("<br/>");
                            //stringBuilder.Append(fileUrlText);
                            //emailNotificationDetails.MailBody = stringBuilder.ToString();
                            emailNotificationDetails.MailBody = emailNotificationDetails.MailBody.Replace("#fileUrlText", fileUrlText);

                        }
                        else
                        {
                            emailNotificationDetails.MailBody = emailNotificationDetails.MailBody.Replace("#fileUrlText", "");
                        }

                    }
                    else
                    {
                        emailNotificationDetails.MailBody = emailNotificationDetails.MailBody.Replace("#fileUrlText", "");
                    }
                    isEmailSent = await EmailNotificationHelper.SendMail(emailNotificationDetails.MailBody, emailNotificationDetails.SendToEmail, emailNotificationDetails.FailureSubject, emailNotificationDetails.FromAddress);
                    if (isEmailSent)
                    {
                        foreach (var tab in multisheetEmail.TabNames.Where(x => !x.SuccessProcess))
                            await _emailNotificationRepository.CommitEmailNotification(emailNotificationDto.FlpConfigurationId, emailNotificationDto.UploadFileId, isEmailSent, tab.TabName);
                    }
                   
                }

                //Update isSentEmail false

               

                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Email sent successfully." },
                    Result = true
                };
            }
            catch (Exception ex)
            {
                // await _emailNotificationRepository.CommitEmailNotification(emailNotificationDto.FlpConfigurationId, emailNotificationDto.UploadFileId, isEmailSent);
                //throw new Exception(ex.Message.ToString());
                _logger.LogError($"Email Error: {ex.Message.ToString()}");

                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { $"Error occurred:{ex.Message.ToString()}" },
                    Result = false
                };
            }
        }

        public async Task<EmailNotificationDetails?> GetEmailTemplateDetails(EmailNotificationDto emailNotificationDto)
        {
            EmailNotificationDetails emailTemplateDetails = null;
            var emailNotificationTemplate = await _cache.GetEmailTemplateListAsync();
            if (emailNotificationTemplate == null)
            {
                _logger.LogInformation("No template found.");
                return emailTemplateDetails;
            }

            if (string.Compare(emailNotificationTemplate?.Result, "Failure", true) == 0)
            {
                _logger.LogError("Return failure from database.");
                return emailTemplateDetails;
            }

            emailTemplateDetails = new EmailNotificationDetails();
            emailTemplateDetails.FromAddress = emailNotificationTemplate.FromAddress;
            string mailBody = "";
            if (emailNotificationDto.FlpProcessStatusId == (int)FlpProcessStatusEnum.Error)
            {
                emailTemplateDetails.FailureSubject = emailNotificationTemplate.FailureSubject;
                emailTemplateDetails.FailureEmailToBeSent = emailNotificationTemplate.FailureSentEmail;
                mailBody = EmailTemplate.FailureEmailTemplate()
                    .Replace("#Error", emailNotificationDto.Error)
                    .Replace("#Status", emailNotificationDto.Status)
                    .Replace("#Stage", emailNotificationDto.Stage)
                    .Replace("#BackupFileId", emailNotificationDto.BackUpFileDetailsId);
            }
            else
            {
                //mailBody = emailNotificationTemplate.SuccessEmailTemplate;
                mailBody = EmailTemplate.SuccessEmailTemplate();
                emailTemplateDetails.SuccessSubject = emailNotificationTemplate.SuccessSubject;
                emailTemplateDetails.SuccessEmailToBeSent = emailNotificationTemplate.SuccessSentEmail;
            }

            if (emailNotificationDto.TotalRecords == "0" && emailNotificationDto.ProcessedRecords == "0" && emailNotificationDto.DuplicateRecords == "0")
            {
                emailNotificationDto.TotalRecords = "NA";
                emailNotificationDto.ProcessedRecords = "NA";
                emailNotificationDto.DuplicateRecords = "NA";
            }

            mailBody = mailBody.Replace("#FileName", emailNotificationDto.FileName)
                      .Replace("#File_Description", emailNotificationDto.Description)
                      .Replace("#TotalRecords", emailNotificationDto.TotalRecords)
                      .Replace("#ProcessedRecords", emailNotificationDto.ProcessedRecords)
                      .Replace("#DuplicateRecords", emailNotificationDto.DuplicateRecords)
                      .Replace("#StartTime", emailNotificationDto.StartTime)
                      .Replace("#EndTime", emailNotificationDto.EndTime)
                      .Replace("#TotalDuration", emailNotificationDto.TotalDuration)
                      .Replace("#Region", emailNotificationDto.Region)
                      .Replace("#SubRegion", emailNotificationDto.SubRegion)
                      .Replace("#Client", emailNotificationDto.Client);
            emailTemplateDetails.MailBody = mailBody;
            emailTemplateDetails.SendToEmail = emailNotificationDto.SendToEmail;

            return emailTemplateDetails;
        }

        public async Task<EmailNotificationDetails?> GetLandingLayerEmailTemplateDetails(EmailNotificationDto emailNotificationDto)
        {
            EmailNotificationDetails emailTemplateDetails = null;
            var emailNotificationTemplate = await _cache.GetEmailTemplateListAsync();
            if (emailNotificationTemplate == null)
            {
                _logger.LogInformation("No template found.");
                return emailTemplateDetails;
            }

            if (string.Compare(emailNotificationTemplate?.Result, "Failure", true) == 0)
            {
                _logger.LogError("Return failure from database.");
                return emailTemplateDetails;
            }

            emailTemplateDetails = new EmailNotificationDetails();
            emailTemplateDetails.FromAddress = emailNotificationTemplate.FromAddress;
            string mailBody = "";
            if (emailNotificationDto.FlpProcessStatusId == (int)FlpProcessStatusEnum.Error)
            {
                emailTemplateDetails.FailureSubject = emailNotificationTemplate.FailureSubject;
                emailTemplateDetails.FailureEmailToBeSent = emailNotificationTemplate.FailureSentEmail;
                mailBody = EmailTemplate.FailureEmailTemplateToLandingLayer()
                    .Replace("#Error", emailNotificationDto.Error)
                    .Replace("#Status", emailNotificationDto.Status)
                    .Replace("#Stage", emailNotificationDto.Stage);
            }
            else
            {                
                mailBody = EmailTemplate.SuccessEmailTemplateToLandingLayer().Replace("#TotalFiles", emailNotificationDto.TotalFileCount.ToString())
                    .Replace("#TotalSuccessMovedFile", emailNotificationDto.SuccessCount.ToString()).Replace("#TotalRejectedFile", emailNotificationDto.FailureCount.ToString());
                emailTemplateDetails.SuccessSubject = emailNotificationTemplate.SuccessSubject;
                emailTemplateDetails.SuccessEmailToBeSent = emailNotificationTemplate.SuccessSentEmail;
            }           
            mailBody = mailBody.Replace("#ProcessName", emailNotificationDto.ConfigurationName)
                      .Replace("#FileDescription", emailNotificationDto.Description)                    
                     // .Replace("#StartTime", emailNotificationDto.StartTime)
                      .Replace("#EndTime", emailNotificationDto.EndTime)
                      .Replace("#Region", emailNotificationDto.Region)
                      .Replace("#SubRegion", emailNotificationDto.SubRegion)
                      .Replace("#Client", emailNotificationDto.Client);
            emailTemplateDetails.MailBody = mailBody;
            emailTemplateDetails.SendToEmail = emailNotificationDto.SendToEmail;
            return emailTemplateDetails;
        }
        private async Task<APIResponse<bool>> SendSingleSheetTemplate(EmailNotificationDto emailNotification)
        {
            bool isEmailSent = false;
            try
            {
                var emailNotificationDetails = new EmailNotificationDetails();
                if(emailNotification.FileProcessingServerTypeId == (int)FileProcessingServerType.LandingLayer)
                {
                    emailNotificationDetails = await GetLandingLayerEmailTemplateDetails(emailNotification);
                }
                else
                    emailNotificationDetails = await GetEmailTemplateDetails(emailNotification);

                if (emailNotificationDetails == null)
                {
                    string message = $"Error: Not sent  email for this {emailNotification.FlpConfigurationId} and config due empty template details";
                    _logger.LogError(message);
                    return new APIResponse<bool>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { message },
                        Result = false
                    };
                }
                if (!emailNotificationDetails.SuccessEmailToBeSent && !emailNotificationDetails.FailureEmailToBeSent)
                {
                    string message = $"Message: Sent Email is off at this moment";
                    _logger.LogInformation(message);
                    return new APIResponse<bool>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { message },
                        Result = false
                    };
                }

                if (emailNotification.FlpProcessStatusId == (int)FlpProcessStatusEnum.Processed)
                {
                    if (emailNotificationDetails.SuccessEmailToBeSent)
                    {
                        isEmailSent = await EmailNotificationHelper.SendMail(emailNotificationDetails.MailBody, emailNotificationDetails.SendToEmail, emailNotificationDetails.SuccessSubject, emailNotificationDetails.FromAddress);
                    }

                }
                else if (emailNotification.FlpProcessStatusId == (int)FlpProcessStatusEnum.Error)
                {


                    if (emailNotificationDetails.FailureEmailToBeSent)
                    {
                        if (emailNotification.FileProcessingServerTypeId != (int)FileProcessingServerType.LandingLayer)
                        {
                            if (!string.IsNullOrWhiteSpace(emailNotification.BlobName))
                            {
                                string sourceBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(emailNotification.SourceStorageAccount, emailNotification.SourceStorageAccountKey);


                                BlobClient blobClient = _iBlobStorageService.GetBlobClientDetails(emailNotification.BlobName, sourceBlobConnectionString, emailNotification.sourceContainerName);

                                string fileUrlText = EmailNotificationHelper.GetSasUri(blobClient);
                                if (!string.IsNullOrWhiteSpace(fileUrlText))
                                {
                                    //var stringBuilder = new StringBuilder();
                                    //stringBuilder.Append(emailNotificationDetails.MailBody);
                                    //stringBuilder.Append("<br/>");
                                    //stringBuilder.Append(fileUrlText);
                                    //emailNotificationDetails.MailBody = stringBuilder.ToString();
                                    emailNotificationDetails.MailBody = emailNotificationDetails.MailBody.Replace("#fileUrlText", fileUrlText);

                                }

                            }
                        }
                        


                        isEmailSent = await EmailNotificationHelper.SendMail(emailNotificationDetails.MailBody, emailNotificationDetails.SendToEmail, emailNotificationDetails.FailureSubject, emailNotificationDetails.FromAddress);
                    }
                }
                //Update isSentEmail false
                await _emailNotificationRepository.CommitEmailNotification(emailNotification.FlpConfigurationId, emailNotification.UploadFileId, isEmailSent,null);

                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Email sent successfully." },
                    Result = true
                };
            }
            catch (Exception ex)
            {
                // await _emailNotificationRepository.CommitEmailNotification(emailNotificationDto.FlpConfigurationId, emailNotificationDto.UploadFileId, isEmailSent);
                //throw new Exception(ex.Message.ToString());
                _logger.LogError($"Email Error: {ex.Message.ToString()}");

                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { $"Error occurred:{ex.Message.ToString()}" },
                    Result = false
                };
            }
        }

        public async Task<EmailNotificationDetails?> GetMultisheetEmailTemplateForSuccess(MultisheetEmailNotificationDto multisheetEmailNotification)
        {
            EmailNotificationDetails emailTemplateDetails = null;
            var emailNotificationTemplate = await _cache.GetEmailTemplateListAsync();
            if (emailNotificationTemplate == null)
            {
                _logger.LogInformation("No template found.");
                return emailTemplateDetails;
            }

           

            if (string.Compare(emailNotificationTemplate?.Result, "Failure", true) == 0)
            {
                _logger.LogError("Return failure from database.");
                return emailTemplateDetails;
            }
            //Success Process Template
            var multisheetEmail = multisheetEmailNotification;
            var flpConfigurationId = multisheetEmail.FlpConfigurationId;
            var fileName = multisheetEmail.FileName;
            var uploadFileId = multisheetEmail.UploadFileId;
            var clientName = multisheetEmail.ClientName;
            var region = multisheetEmail.Region;
            var subRegion = multisheetEmail.SubRegion;
            var description = multisheetEmail.Description;
            var sentToEmail = multisheetEmail.SentToEmail;
            var successTabDetailsList = multisheetEmail.TabNames.Where(x=>x.SuccessProcess);
          

            //multisheetEmailNotification
            emailTemplateDetails = new EmailNotificationDetails();
            emailTemplateDetails.FromAddress = emailNotificationTemplate.FromAddress;
            string mailBody = "";
            //mailBody = emailNotificationTemplate.SuccessEmailTemplate;
            mailBody = EmailTemplate.SuccessEmailTemplate();
            emailTemplateDetails.SuccessSubject = emailNotificationTemplate.SuccessSubject;
            emailTemplateDetails.SuccessEmailToBeSent = emailNotificationTemplate.SuccessSentEmail;

            mailBody = EmailTemplate.MultisheetSuccessEmailTemplate();
            emailTemplateDetails.SuccessSubject = emailNotificationTemplate.SuccessSubject;
            emailTemplateDetails.SuccessEmailToBeSent = emailNotificationTemplate.SuccessSentEmail;
            mailBody = mailBody.Replace("#FileName", fileName)
                   .Replace("#File_Description", description)
                   .Replace("#Region", region)
                   .Replace("#SubRegion", subRegion)
                   .Replace("#Client", clientName);

           
            string tabDetailsTemplateForSuccess = "";

            foreach (var tab in successTabDetailsList)
            {
                var tabName = tab.TabName;
                var emailNotificationDto = tab.NotificationDetail;
                if (emailNotificationDto.TotalRecords == "0" && emailNotificationDto.ProcessedRecords == "0" && emailNotificationDto.DuplicateRecords == "0")
                {
                    emailNotificationDto.TotalRecords = "NA";
                    emailNotificationDto.ProcessedRecords = "NA";
                    emailNotificationDto.DuplicateRecords = "NA";
                }
                tabDetailsTemplateForSuccess += EmailTemplate.MultisheetSuccessTabDetailsTemplate()
                    .Replace("#TabName", tabName)
                    .Replace("#TotalRecords", emailNotificationDto.TotalRecords)
                    .Replace("#ProcessedRecords", emailNotificationDto.ProcessedRecords)
                    .Replace("#DuplicateRecords", emailNotificationDto.DuplicateRecords)
                    .Replace("#StartTime", emailNotificationDto.StartTime)
                    .Replace("#EndTime", emailNotificationDto.EndTime);
               
            }

            mailBody = mailBody.Replace("#tabDetails", tabDetailsTemplateForSuccess);



            emailTemplateDetails.MailBody = mailBody;
            emailTemplateDetails.SendToEmail = sentToEmail;

            return emailTemplateDetails;
        }


        public async Task<EmailNotificationDetails?> GetMultisheetEmailTemplateForFailure(MultisheetEmailNotificationDto multisheetEmailNotification)
        {
            EmailNotificationDetails emailTemplateDetails = null;
            var emailNotificationTemplate = await _cache.GetEmailTemplateListAsync();
            if (emailNotificationTemplate == null)
            {
                _logger.LogInformation("No template found.");
                return emailTemplateDetails;
            }

            if (string.Compare(emailNotificationTemplate?.Result, "Failure", true) == 0)
            {
                _logger.LogError("Return failure from database.");
                return emailTemplateDetails;
            }
            //Failure Process Template
            var multisheetEmail = multisheetEmailNotification;
            var flpConfigurationId = multisheetEmail.FlpConfigurationId;
            var fileName = multisheetEmail.FileName;
            var uploadFileId = multisheetEmail.UploadFileId;
            var clientName = multisheetEmail.ClientName;
            var region = multisheetEmail.Region;
            var subRegion = multisheetEmail.SubRegion;
            var description = multisheetEmail.Description;
            var sentToEmail = multisheetEmail.SentToEmail;
            var failureTabDetailsList = multisheetEmail.TabNames.Where(x => !x.SuccessProcess);


            //multisheetEmailNotification
            emailTemplateDetails = new EmailNotificationDetails();
            emailTemplateDetails.FromAddress = emailNotificationTemplate.FromAddress;
            string mailBody = "";
            //mailBody = emailNotificationTemplate.SuccessEmailTemplate;
            mailBody = EmailTemplate.MultisheetFailureEmailTemplate();
            emailTemplateDetails.FailureSubject = emailNotificationTemplate.FailureSubject;
            emailTemplateDetails.FailureEmailToBeSent = emailNotificationTemplate.FailureSentEmail;

          //  mailBody = EmailTemplate.MultisheetFailureTabDetailsTemplate();
           // emailTemplateDetails.SuccessSubject = emailNotificationTemplate.SuccessSubject;
           // emailTemplateDetails.SuccessEmailToBeSent = emailNotificationTemplate.SuccessSentEmail;
            mailBody = mailBody.Replace("#FileName", fileName)
                   .Replace("#File_Description", description)
                   .Replace("#Region", region)
                   .Replace("#SubRegion", subRegion)
                   .Replace("#Client", clientName);



            string tabDetailsTemplateForFailure = "";

            foreach (var tab in failureTabDetailsList)
            {
                var tabName = tab.TabName;
                var emailNotificationDto = tab.NotificationDetail;
                if (emailNotificationDto.TotalRecords == "0" && emailNotificationDto.ProcessedRecords == "0" && emailNotificationDto.DuplicateRecords == "0")
                {
                    emailNotificationDto.TotalRecords = "NA";
                    emailNotificationDto.ProcessedRecords = "NA";
                    emailNotificationDto.DuplicateRecords = "NA";
                }
                tabDetailsTemplateForFailure += EmailTemplate.MultisheetFailureTabDetailsTemplate()
                    .Replace("#TabName", tabName)
                    .Replace("#TotalRecords", emailNotificationDto.TotalRecords)
                    .Replace("#ProcessedRecords", emailNotificationDto.ProcessedRecords)
                    .Replace("#DuplicateRecords", emailNotificationDto.DuplicateRecords)
                    .Replace("#StartTime", emailNotificationDto.StartTime)
                    .Replace("#EndTime", emailNotificationDto.EndTime)                    
                   // .Replace("#Status", emailNotificationDto.Status)
                    .Replace("#Stage", emailNotificationDto.Stage)
                    .Replace("#BackupFileId", emailNotificationDto.BackUpFileDetailsId)
                    .Replace("#Error", emailNotificationDto.Error);

            }

            mailBody = mailBody.Replace("#tabDetails", tabDetailsTemplateForFailure);



            emailTemplateDetails.MailBody = mailBody;
            emailTemplateDetails.SendToEmail = sentToEmail;

            return emailTemplateDetails;
        }





    }
}
