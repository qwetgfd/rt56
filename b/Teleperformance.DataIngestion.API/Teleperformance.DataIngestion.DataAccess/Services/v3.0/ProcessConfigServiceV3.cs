using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Spreadsheet;
using ExcelDataReader;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Converters;
using NPOI.HSSF.UserModel;
using NPOI.OpenXmlFormats.Spreadsheet;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Parquet.Schema;
using System.IdentityModel.Tokens.Jwt;
using System.IO.Compression;
using System.Security.Claims;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.DataAccess.Services.v2._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Account;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;

using Teleperformance.DataIngestion.Models.Entities.v2._0;
using Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration;


namespace Teleperformance.DataIngestion.DataAccess.Services.v3._0
{
    public class ProcessConfigServiceV3 : IProcessConfigServiceV3
    {
        private readonly IProcessConfigRepositoryV3 processConfigRepository;
        private readonly ILogger<ProcessConfigServiceV3> logger;
        private readonly IHeaderService headerService;
        private Dictionary<string, ICellStyle> styleCache = new();

        public ProcessConfigServiceV3(IProcessConfigRepositoryV3 processConfigRepository, ILogger<ProcessConfigServiceV3> logger, IHeaderService headerService)
        {
            this.processConfigRepository = processConfigRepository;
            this.logger = logger;
            this.headerService = headerService;
        }

        public async Task<APIResponse<string>> ConvertEnglishCharactersOnly(string wordToConvert, string language)
        {

            string errorMessage = string.Empty;

            //string securityGroupId = headerService.GetHeaderValue("x-tpdi-api-sg");

            //if (string.IsNullOrWhiteSpace(securityGroupId))
            //{
            //    logger.LogError("Error: Security group not found.");
            //    return new APIResponse<string>
            //    {
            //        ResultStatus = APIResultStatus.InvalidParameters,
            //        ResponseMessage = new List<string> { "Security group not found." },
            //        Result = null
            //    };
            //}

            if (string.IsNullOrWhiteSpace(wordToConvert))
            {
                errorMessage = "No word to convert.";
                logger.LogInformation(errorMessage);
                return await Task.FromResult(new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.NoContent,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = null
                });
            }

            if (string.IsNullOrWhiteSpace(language))
            {
                errorMessage = "Language is required.";
                logger.LogInformation(errorMessage);
                return await Task.FromResult(new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.NoContent,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = null
                });
            }

            var englishCharacters = await processConfigRepository.GetEnglishEquivalents(language);

            if (englishCharacters != null)
            {
                foreach (char c in wordToConvert)
                {
                    var foundEquivalent = englishCharacters.FirstOrDefault(letter => letter.CharToConvert == c.ToString());
                    if (foundEquivalent != null)
                    {
                        wordToConvert = wordToConvert.Replace(c.ToString(), foundEquivalent.EnglishEquivalent);
                    }

                }
            }

            return await Task.FromResult(new APIResponse<string>
            {

                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { errorMessage },
                Result = wordToConvert
            });

        }

        public async Task<APIResponse<EnableDisableProcessByFlpConfigurationIdResponseDto>> EnableDisableProcessByConfigurationId(string flpConfigurationIds, string userName, string created_by, bool activeStatus)
        {
            string errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(userName))
            {
                errorMessage = "UserName is required.";
                logger.LogError(errorMessage);
                return await Task.FromResult(new APIResponse<EnableDisableProcessByFlpConfigurationIdResponseDto>
                {
                    ResultStatus = APIResultStatus.NoContent,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = null
                });
            }

            if (string.IsNullOrWhiteSpace(flpConfigurationIds))
            {
                errorMessage = "Configuration Id is required.";
                logger.LogError(errorMessage);
                return await Task.FromResult(new APIResponse<EnableDisableProcessByFlpConfigurationIdResponseDto>
                {
                    ResultStatus = APIResultStatus.NoContent,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = null
                });
            }

            var dbResponse = await processConfigRepository.EnableDisableProcessByConfigurationId(flpConfigurationIds, userName, created_by, activeStatus);

            if (string.Compare(dbResponse.Result, "Error", true) == 0)
                return await Task.FromResult(new APIResponse<EnableDisableProcessByFlpConfigurationIdResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { dbResponse.Message },
                    Result = null
                });

            return await Task.FromResult(new APIResponse<EnableDisableProcessByFlpConfigurationIdResponseDto>
            {

                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { errorMessage },
                Result = dbResponse
            });
        }

        public async Task<APIResponse<List<EnglishCharactersEquivalents>>> GetAllEnglishCharactersOnly(string language)
        {

            string errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(language))
            {
                errorMessage = "Language is required.";
                logger.LogInformation(errorMessage);
                return await Task.FromResult(new APIResponse<List<EnglishCharactersEquivalents>>
                {
                    ResultStatus = APIResultStatus.NoContent,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = null
                });
            }

            var englishCharacters = await processConfigRepository.GetEnglishEquivalents(language);



            return await Task.FromResult(new APIResponse<List<EnglishCharactersEquivalents>>
            {

                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { errorMessage },
                Result = englishCharacters
            });
        }

        public async Task<APIResponse<List<DIDatabaseNameDto>>> GetDeltaDatabaseNames(int regionId, string subRegionId, int clientNameId)
        {
            try
            {
                string securityGroupId = headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    logger.LogError("Error: Security group not found.");
                    return new APIResponse<List<DIDatabaseNameDto>>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = null
                    };
                }
                var retFromDb = await processConfigRepository.GetDIDeltaDatabaseNames(regionId, subRegionId, clientNameId, securityGroupId);
                if (retFromDb == null)
                {
                    logger.LogError("Error: Delta Database list not found.");
                    return new APIResponse<List<DIDatabaseNameDto>>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "Delta Database list not found." },
                        Result = null
                    };
                }
                var databaseNames =
                    retFromDb.Select(x => new DIDatabaseNameDto()
                    {
                        Id = x.Id,
                        DatabaseName = x.DatabaseName,
                        DatabaseServer = x.DatabaseServer,
                        DefaultDB = x.defaultDB
                    }).ToList();
                return new APIResponse<List<DIDatabaseNameDto>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = databaseNames
                };
            }
            catch (Exception ex)
            {
                //TODO:
                this.logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<UpdateLoginResponseDto>> UpdateLogin(UpdateLoginDto updateLoginDto, ClaimsPrincipal user, CancellationToken ct)
        {

            string errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(updateLoginDto.LoginId))
            {
                errorMessage = "LoginId is required.";
                logger.LogError(errorMessage);
                return await Task.FromResult(new APIResponse<UpdateLoginResponseDto>
                {
                    ResultStatus = APIResultStatus.NoContent,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = null
                });
            }           

            var dbResponse = await processConfigRepository.UpdateLogin(updateLoginDto.LoginId);

            if (string.Compare(dbResponse.Result, "Error", true) == 0)
                return await Task.FromResult(new APIResponse<UpdateLoginResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { dbResponse.Message },
                    Result = null
                });



            
            var jti = user.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            var expUnix = user.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

            if (!string.IsNullOrEmpty(jti) && long.TryParse(expUnix, out var expUnixLong))
            {
                var expAt = DateTimeOffset.FromUnixTimeSeconds(expUnixLong);
             
                var revokedToken = await processConfigRepository.RevokeAsync(jti, expAt);
            }


            return await Task.FromResult(new APIResponse<UpdateLoginResponseDto>
            {

                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { errorMessage },
                Result = dbResponse
            });
        }
        //Commented this method as per new requirement currently this code is skipped in UI
        //public async Task<APIResponse<ConvertToXLSXDto>> ConvertToXLSX(IFormFile file, Stream stream, string processName)
        //{
        //    using var outputStream = new MemoryStream();
        //    int maxRowsPerSheet = 10000;
        //    int totalRowsInTheSheet = 0;

        //    try
        //    {


        //        if (Path.GetExtension(file.FileName).ToLowerInvariant() == ".xls")
        //        {

        //            var hssfWorkbook = new HSSFWorkbook(stream); // Read .xls                    
        //            var xssfWorkbook = new XSSFWorkbook();       // Create .xlsx            

        //            int sheetIndex = 0;

        //            for (int sheetNum = 0; sheetNum < hssfWorkbook.NumberOfSheets; sheetNum++)
        //            {
        //                var sheet = hssfWorkbook.GetSheetAt(sheetNum);
        //                int totalRows = Math.Min(sheet.LastRowNum + 1, maxRowsPerSheet); // Cap at 10,000
        //                var newSheet = xssfWorkbook.CreateSheet(sheet.SheetName); // Keep original sheet name

        //                totalRowsInTheSheet = hssfWorkbook.GetSheetAt(0).LastRowNum + 1;
        //                //apply dummy style to force styles.xml creation
        //                var dummyStyle = xssfWorkbook.CreateCellStyle();
        //                dummyStyle.WrapText = false;

        //                for (int i = 0; i < totalRows; i++)
        //                {
        //                    var oldRow = sheet.GetRow(i);
        //                    if (oldRow == null) continue;

        //                    var newRow = newSheet.CreateRow(i);
        //                    foreach (var oldCell in oldRow.Cells)
        //                    {
        //                        var newCell = newRow.CreateCell(oldCell.ColumnIndex);
        //                        newCell.CellStyle = dummyStyle;
        //                        CopyCellValue(oldCell, newCell);
        //                    }
        //                }
        //            }

        //            //using var outputStream = new MemoryStream();
        //            xssfWorkbook.Write(outputStream);


        //            //return await Task.FromResult(new APIResponse<byte[]>
        //            //{
        //            //    ResultStatus = APIResultStatus.Completed,
        //            //    ResponseMessage = new List<string>(),
        //            //    Result = outputStream.ToArray() // Single workbook as byte array
        //            //});
        //        }
        //        else
        //        {
        //            // Aspose.Cells logic for .xlsb
        //            var sourceWorkbook = new Aspose.Cells.Workbook(stream); // Load .xlsb from stream
        //            var targetWorkbook = new Aspose.Cells.Workbook(); // Create new .xlsx workbook
        //            targetWorkbook.Worksheets.Clear(); // remove default worksheet

        //            //get the total number of rows
        //            var sheet1 = sourceWorkbook.Worksheets[0];
        //            totalRowsInTheSheet = sheet1.Cells.MaxDataRow + 1;

        //            //using var outputStream = new MemoryStream();
        //            //sourceWorkbook.Save(outputStream, Aspose.Cells.SaveFormat.Xlsx); // Save as .xlsx
        //            //convertedBytes = outputStream.ToArray();


        //            foreach (Aspose.Cells.Worksheet sourceSheet in sourceWorkbook.Worksheets)
        //            {
        //                var targetSheet = targetWorkbook.Worksheets.Add(sourceSheet.Name);

        //                int rowCount = Math.Min(sourceSheet.Cells.MaxDataRow + 1, maxRowsPerSheet);
        //                int colCount = sourceSheet.Cells.MaxDataColumn + 1;

        //                for (int row = 0; row < rowCount; row++)
        //                {
        //                    for (int col = 0; col < colCount; col++)
        //                    {
        //                        var cell = sourceSheet.Cells[row, col];
        //                        if (cell != null && cell.Type != CellValueType.IsNull)
        //                        {
        //                            if(cell.Type == CellValueType.IsDateTime || IsDateFormatted(cell))
        //                            {
        //                                DateTime dateValue = cell.DateTimeValue;
        //                                Style sourceStyle = cell.GetStyle();
        //                                Style targetStyle = targetSheet.Cells[row, col].GetStyle();
        //                                targetStyle.Copy(sourceStyle);
        //                                targetSheet.Cells[row, col].SetStyle(targetStyle);
        //                                targetSheet.Cells[row, col].PutValue(dateValue);
        //                            } else
        //                            {
        //                                targetSheet.Cells[row, col].PutValue(cell.Value);
        //                            }

        //                        }
        //                    }
        //                }
        //            }

        //            targetWorkbook.Save(outputStream, Aspose.Cells.SaveFormat.Xlsx);


        //        }
        //        var ret = new ConvertToXLSXDto
        //        {
        //            fileData = Convert.ToBase64String(outputStream.ToArray()),
        //            rowCount = totalRowsInTheSheet
        //        };
        //        return await Task.FromResult(new APIResponse<ConvertToXLSXDto>
        //        {
        //            ResultStatus = APIResultStatus.Completed,
        //            ResponseMessage = new List<string>(),
        //            Result = ret // Single workbook as byte array
        //        });
        //    }
        //    catch (Exception ex)
        //    {

        //        throw ex;
        //    }


        //}

        //private static bool IsDateFormatted(Cell cell)
        //{
        //    Style style = cell.GetStyle();
        //    return style.Number >= 14 && style.Number <= 22; // Excel's built-in date formats
        //}

        //private void CopyCellValue(ICell oldCell, ICell newCell)
        //{



        //    try
        //    {
        //        switch (oldCell.CellType)
        //        {
        //            case CellType.String:
        //                newCell.SetCellValue(oldCell.StringCellValue);
        //                break;
        //            case CellType.Numeric:
        //                if (HSSFDateUtil.IsCellDateFormatted(oldCell))
        //                {
        //                    var dateValue = oldCell.DateCellValue;
        //                    ((XSSFCell)newCell).SetCellValue(dateValue);

        //                    var originalFormatIndex = oldCell.CellStyle.DataFormat;
        //                    var originalFormatString = oldCell.Sheet.Workbook.CreateDataFormat().GetFormat(originalFormatIndex);
        //                    var formatString = oldCell.CellStyle.GetDataFormatString();

        //                    var workbook = newCell.Sheet.Workbook as XSSFWorkbook;
        //                    if (!styleCache.TryGetValue(originalFormatString, out var cachedStyle))
        //                    {
        //                        var dateStyle = workbook.CreateCellStyle();
        //                        var format = workbook.CreateDataFormat();
        //                        dateStyle.DataFormat = format.GetFormat(originalFormatString);

        //                        styleCache[originalFormatString] = dateStyle;
        //                        cachedStyle = dateStyle;
        //                    }

        //                    newCell.CellStyle = cachedStyle;


        //                }
        //                else
        //                {
        //                    newCell.SetCellValue(oldCell.NumericCellValue);
        //                }
        //                break;
        //            case CellType.Boolean:
        //                newCell.SetCellValue(oldCell.BooleanCellValue);
        //                break;
        //            case CellType.Formula:
        //                newCell.SetCellFormula(oldCell.CellFormula);
        //                break;
        //            case CellType.Error:
        //                newCell.SetCellErrorValue(oldCell.ErrorCellValue);
        //                break;
        //            case CellType.Blank:
        //                newCell.SetCellType(CellType.Blank);
        //                break;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.Message);
        //        throw;
        //    }

        //}
    }


}
