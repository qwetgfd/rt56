FileUrl = BuildSharePointFilePath(item.Path, item.WebUrl, item.Name),
                                FileName = item.Name,


private static string BuildSharePointFilePath(string? path, string? webUrl, string? name)
        {
            var fileName = name?.Trim() ?? "";
            var folderPath = path?.Trim().TrimStart('/').TrimEnd('/') ?? "";
            if (!string.IsNullOrEmpty(folderPath))
            {
                if (!string.IsNullOrEmpty(fileName) && folderPath.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                    return folderPath;
                if (!string.IsNullOrEmpty(fileName) && string.IsNullOrEmpty(Path.GetExtension(folderPath)))
                    return $"{folderPath}/{fileName}";
                return folderPath;
            }
            if (!string.IsNullOrWhiteSpace(webUrl))
                return webUrl.Trim();
            return fileName;
        }












 var sharePointFileName = !string.IsNullOrWhiteSpace(flpRequestDto.SharePointFileLocation.FileName)
                        ? flpRequestDto.SharePointFileLocation.FileName
                        : flpRequestDto.SharePointFileLocation.FileUrl;
                    var isValidFile = await _fileLoadingProcessConfiguration.IsValidFile(flpConfigurationRequestDto.FlpConfigurationId, sharePointFileName, ".txt");















#region SharePoint Workspace - AY
        [JsonProperty("sharePointFiles")]
        public List<SharePointFileLocation> SharePointFiles { get; set; }
        #endregion





         #region SharePoint Workspace - AY
    public class SharePointFileLocation
    {
        public string UploadedId { get; set; }
        public string FileUrl { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
    }
    #endregion















     #region SharePoint Workspace - AY
                        if (flp.LocationTypeId == (int)LocationTypeEnum.SharePoint)
                        {
                            if (flp.SharePointFiles != null && flp.SharePointFiles.Any())
                            {
                                foreach (var spFile in flp.SharePointFiles)
                                {
                                    var flpConvertToParquetRequest = new FlpConvertToParquetRequest();
                                    flpConvertToParquetRequest.SharePointFileLocation = spFile;
                                    flpConvertToParquetRequest.FlpConfigurationId = flp.FlpConfigurationId;
                                    flpConvertToParquetRequest.ProcessName = flp.ProcessName;
                                    var schedulerApiUrl = GetImportSchedulerApiUrl(spFile.FileName, spFile.FileUrl);
                                    if (string.IsNullOrEmpty(schedulerApiUrl))
                                    {
                                        await new DataRepository().LogError($"Function App: Unsupported SharePoint file type for {spFile.FileName ?? spFile.FileUrl} at {DateTime.UtcNow}", "Function App", "Error");
                                        continue;
                                    }
                                    if (flp.FileProcessingServerTypeId == (int)ServerTypeEnum.DataLakeType)
                                        tasks.Add(FlpConvertToParquet(apiUrl, "4.0", token, schedulerApiUrl, flpConvertToParquetRequest));
                                    else
                                        tasks.Add(FlpConvertToParquet(apiUrl, "3.0", token, schedulerApiUrl, flpConvertToParquetRequest));
                                }
                            }
                            else
                            {
                                tasks.Add(UpdateProcessSchedulerLastDate(apiUrl, apiVersion, token, new FlpConvertToParquetRequest { FlpConfigurationId = flp.FlpConfigurationId }));
                            }
                        }
                        #endregion
