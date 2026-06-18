using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Parquet.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using TPMailerService;

namespace Teleperformance.DataIngestion.DataAccess.Helpers
{
    public class EmailNotificationHelper
    {

        public string SuccessEmailTemplate()
        {
            string mailBody = @"<!DOCTYPE html>
                                        <html>
                                        <head>
                                            <style>
                                                body {
                                                    font-family: Arial, sans-serif;
                                                    margin: 20px;
                                                    line-height: 1.6;
                                                }
                                                .container {
                                                    width: 100%;
                                                    max-width: 600px;
                                                    margin: 0 auto;
                                                    border: 1px solid #ccc;
                                                    border-radius: 8px;
                                                    padding: 20px;
                                                    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
                                                }
                                                .header {
                                                    font-size: 18px;
                                                    font-weight: bold;
                                                    margin-bottom: 20px;
                                                }
                                                .details {
                                                    margin: 10px 0;
                                                }
                                                .details p {
                                                    margin: 5px 0;
                                                }
                                            </style>
                                        </head>
                                        <body>
                                            <div class=""container"">
                                                <div class=""header"">
                                                    The file has been processed successfully. Below are the details:
                                                </div>
                                                <div class=""details"">
                                                    <p><strong>File Name:</strong>#FileName</p>
                                                    <p><strong>Total Records:</strong>#TotalRecords</p>
                                                    <p><strong>Processed Records:</strong>#ProcessedRecords</p>
                                                    <p><strong>Total Duration:</strong>#TotalRecords</p>
                                                    <p><strong>Start Time:</strong>#StartTime</p>
                                                    <p><strong>End Time:</strong>#EndTime</p>
                                                    <p><strong>Region:</strong>#Region</p>
                                                    <p><strong>Sub Region:</strong>#SubRegion</p>
                                                    <p><strong>Client:</strong>#Client</p>
                                                </div>
                                            </div>
                                        </body>
                                        </html>
                                        ";
            return mailBody;
        }


        public string FailureEmailTemplate()
        {
            string mailBody = @"<!DOCTYPE html>
                                        <html>
                                        <head>
                                            <style>
                                                body {
                                                    font-family: Arial, sans-serif;
                                                    margin: 20px;
                                                    line-height: 1.6;
                                                }
                                                .container {
                                                    width: 100%;
                                                    max-width: 600px;
                                                    margin: 0 auto;
                                                    border: 1px solid #ccc;
                                                    border-radius: 8px;
                                                    padding: 20px;
                                                    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
                                                }
                                                .header {
                                                    font-size: 18px;
                                                    font-weight: bold;
                                                    margin-bottom: 20px;
                                                }
                                                .details {
                                                    margin: 10px 0;
                                                }
                                                .details p {
                                                    margin: 5px 0;
                                                }
                                            </style>
                                        </head>
                                        <body>
                                            <div class=""container"">
                                                <div class=""header"">
                                                Unfortunately, the file could not be processed successfully due to an error:                                  <p>#Error</p> 
                                                Please find the attached file and review the details below:
                                                </div>
                                                <div class=""details"">
                                                    <p><strong>File Name:</strong>#FileName</p>
                                                    <p><strong>Total Records:</strong>#TotalRecords</p>
                                                    <p><strong>Processed Records:</strong>#ProcessedRecords</p>
                                                    <p><strong>Total Duration:</strong>#TotalRecords</p>
                                                    <p><strong>Start Time:</strong>#StartTime</p>
                                                    <p><strong>End Time:</strong>#EndTime</p>
                                                    <p><strong>Region:</strong>#Region</p>
                                                    <p><strong>Sub Region:</strong>#SubRegion</p>
                                                    <p><strong>Client:</strong>#Client</p> <br/>
                                                    <p><strong>Error:</strong>#Error</p>
                                                </div>
                                            </div>
                                        </body>
                                        </html>
                                        ";
            return mailBody;
        }
        public static async Task<bool> SendMail(string mailBody, string sendTo, string subject, string fromAddress,string bcc = "")
        {
            bool isSuccess = true;
            try
            {        

                MailData mailData = new MailData();                
                mailData.FromAddress = fromAddress;
                mailData.MailSubject = subject;
                if (!string.IsNullOrEmpty(bcc))
                {
                    mailData.BCCAddress = bcc;
                }
                mailData.ToAddress = sendTo;
                mailData.IsHTMLBody = true;
                mailData.MailBody = mailBody;
                MailClient mailClient = new MailClient(TPMailerService.MailClient.EndpointConfiguration.NetTcpBinding_IMail);
                await mailClient.SendMailAsync(mailData);
            }
            catch (Exception ex)
            {
                isSuccess = false;
                throw new Exception("Email Error: " + ex.Message, ex);

            }
            return isSuccess;
        }

        public static string GetSasUri(BlobClient blobClient)
        {
            try
            {
                // Set the expiry time for the SAS (e.g., 5 day)
                DateTimeOffset expiryTime = DateTimeOffset.UtcNow.AddDays(5);

                // Generate the SAS URI
                if (blobClient.CanGenerateSasUri)
                {
                    var sasBuilder = new BlobSasBuilder
                    {
                        BlobContainerName = blobClient.BlobContainerName,
                        BlobName = blobClient.Name,
                        Resource = "b",
                        ExpiresOn = expiryTime
                    };
                    sasBuilder.SetPermissions(BlobContainerSasPermissions.Read);
                    Uri sasUri = blobClient.GenerateSasUri(sasBuilder);
                    string mailBody = @$"<p>Please download the uploaded file by using the link below:</p>
          <p><b>Note:<b/>If you are unable to download the file you can email to app support team :<b>TPITApplicationSupport@teleperformance.com</b></p>
          <a href=""{sasUri}"" target=""_blank"">Download File</a>";
                    return mailBody;
                }
                else
                {
                    throw new Exception("SAS generation is not supported.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error downloading blob: {ex.Message}");
            }
        }

        private static string GetMimeType(string extension)
        {
            return extension.ToLower() switch
            {
                ".csv" => "text/csv",
                ".txt" => "text/plain",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".zip" => "application/zip",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => MediaTypeNames.Application.Octet
            };
        }


        private static string GetMimeTypeOld(string extension)
        {
            return extension.ToLower() switch
            {
                ".csv" => "text/csv",
                ".txt" => "text/plain",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".zip" => "application/zip",
                _ => MediaTypeNames.Application.Octet
            };
        }

        static async Task Main(string[] args)
        {
            // Blob URI
            string blobUri = "https://<account-name>.blob.core.windows.net/<container-name>/<blob-name>";

            // Create BlobClient
            BlobClient blobClient = new BlobClient(new Uri(blobUri));

            // Prepare AttachmentFile
            AttachmentFile[] attachmentFiles = new AttachmentFile[1];
            AttachmentFile attachmentFile = new AttachmentFile();

            try
            {
                // Extract blob name for AttachmentFileName
                attachmentFile.AttachmentFileName = Path.GetFileName(blobClient.Name);

                // Set MIME type based on file extension
                string extension = Path.GetExtension(blobClient.Name);
                attachmentFile.AttachmentMIMEType = GetMimeType(extension);

                // Download blob to MemoryStream
                using MemoryStream memStream = new MemoryStream();
                await blobClient.DownloadToAsync(memStream);

                // Assign MemoryStream to AttachmentFile
                attachmentFile.AttachmentMemoryStream = memStream;

                // Add to array
                attachmentFiles[0] = attachmentFile;

                Console.WriteLine("File retrieved successfully:");
                Console.WriteLine($"Name: {attachmentFile.AttachmentFileName}");
                Console.WriteLine($"MIME Type: {attachmentFile.AttachmentMIMEType}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving blob: {ex.Message}");
            }
        }
    }
}
