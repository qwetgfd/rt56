using Azure;
using Dapper;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NLog;
using NPOI.POIFS.Crypt;
using Org.BouncyCastle.Asn1.Cmp;
using Org.BouncyCastle.Pqc.Crypto.Lms;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Enums.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Services.v3._0
{
    public class DataSliceAPIService : IDataSliceAPIService
    {
        
        private readonly ILogger<DataSliceAPIService> _logger;
        private readonly IHeaderService _headerService;
        private readonly IProcessConfigService _processConfigurationService;
        private readonly IMemoryCache _cache;
        private readonly IProcessConfigRepositoryV3 _processConfigRepositoryV3;

        public DataSliceAPIService(IProcessConfigService processConfigurationService, ILogger<DataSliceAPIService> logger, IHeaderService headerService, IMemoryCache cache, IProcessConfigRepositoryV3 processConfigRepositoryV3)
        {
            _processConfigurationService = processConfigurationService;
            _logger = logger;
            _headerService = headerService;
            _cache = cache;
            _processConfigRepositoryV3 = processConfigRepositoryV3;
        }


        public async Task<APIResponse<IEnumerable<DsRegionResponseDto>>> GetRegionBySecurityGroup()
        {
            string errorMessage = "";
            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");

            if (string.IsNullOrWhiteSpace(securityGroupId))
            {
                errorMessage = "GetRegionBySecurityGroup Error:Not found security group in Header";
                _logger.LogError(errorMessage);
                return new APIResponse<IEnumerable<DsRegionResponseDto>>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = new List<DsRegionResponseDto>()//For empty array
                };
            }

            // Fetch data from cache
            var response = await FillDataInCache();
            if (response.ResponseCode != 200 || response.Result == null)
            {
                errorMessage = "GetRegionBySecurityGroup Error:Not found filled data in cache";
                _logger.LogError(errorMessage);
                return new APIResponse<IEnumerable<DsRegionResponseDto>>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = new List<DsRegionResponseDto>()
                };
            }

            var getDataList = response.Result.GetDataList;
            var securityGroupMappingList = response.Result.SecurityGroupMappingsList;
            IEnumerable<DsRegionResponseDto> regionList = new List<DsRegionResponseDto>();
            
            //Check Security group Id exist in mapping table or not 
            if (!securityGroupMappingList.Any(sg => sg.securityGroupId == securityGroupId))
            {
                 errorMessage = $"GetRegionBySecurityGroup Error:Not found SecurityGroupId:{securityGroupId} in mapping cache";
                _logger.LogError(errorMessage);
                return new APIResponse<IEnumerable<DsRegionResponseDto>>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = regionList//null
                };
            }

            //Get the  security group Id details /get all regions by selected security group ids
            //var regionIds = securityGroupMappingList.Where(sg => sg.securityGroupId == securityGroupId && sg.regionId != null).Select(sg => sg.regionId);
            var list = securityGroupMappingList.Where(sg => sg.securityGroupId == securityGroupId).ToList();

            if (list.Any() == true)
            {
               
                var isNullRegionInMapping = list.Any(x => x.regionId == null);

                if(isNullRegionInMapping)
                {
                    // If no specific regionIds, return all distinct regions
                    regionList = getDataList
                        .Select(rg => new DsRegionResponseDto
                        {
                            region_ident = rg.region_ident,
                            region = rg.region
                        })
                        .DistinctBy(rg => rg.region_ident); // Ensures distinct records
                }
                else
                {

                    // Filter records based on provided regionIds
                    var regionIds = list.Where(x => x.regionId != null).Select(x=>x.regionId);

                    regionList = getDataList
                  .Where(rg => regionIds.Contains(rg.region_ident))
                  .Select(rg => new DsRegionResponseDto
                  {
                      region_ident = rg.region_ident,
                      region = rg.region
                  }).DistinctBy(rg => rg.region_ident);
                }
              
            }
           

            return new APIResponse<IEnumerable<DsRegionResponseDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = regionList.OrderBy(rg => rg.region)
            };
        }

        public async Task<APIResponse<IEnumerable<DsSubRegionResponseDto>>> GetSubRegion()
        {
            string errorMessage = "";
            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
            int? regionId = Convert.ToInt32(_headerService.GetHeaderValue("regionId"));

            if (string.IsNullOrWhiteSpace(securityGroupId))
            {
                errorMessage = "GetSubRegion Error:Not found security group in Header";
                _logger.LogError(errorMessage);
                return new APIResponse<IEnumerable<DsSubRegionResponseDto>>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = new List<DsSubRegionResponseDto>()//For empty array
                };
            }
            if (regionId == null)
            {
                errorMessage = "GetSubRegion Error:regionId is null";
                _logger.LogError(errorMessage);
                return new APIResponse<IEnumerable<DsSubRegionResponseDto>>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = new List<DsSubRegionResponseDto>()
                };
            }
            // Fetch data from cache
            var response = await FillDataInCache();
            if (response.ResponseCode != 200 || response.Result == null)
            {
                errorMessage = "GetSubRegion Error:Not found filled data in cache";
                _logger.LogError(errorMessage);
                return new APIResponse<IEnumerable<DsSubRegionResponseDto>>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = new List<DsSubRegionResponseDto>()
                };
            }

            var getDataList = response.Result.GetDataList;
            var securityGroupMappingList = response.Result.SecurityGroupMappingsList;
            IEnumerable<DsSubRegionResponseDto> subRegionList = new List<DsSubRegionResponseDto>();

            //Check Security group Id exist in mapping table or not 
            if (!securityGroupMappingList.Any(sg => sg.securityGroupId == securityGroupId))
            {
                errorMessage = $"GetSubRegion Error:Not found SecurityGroupId:{securityGroupId} in mapping cache";
                _logger.LogError(errorMessage);
                return new APIResponse<IEnumerable<DsSubRegionResponseDto>>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = new List<DsSubRegionResponseDto>()
                };
            }

            
            //If user wants to show the all subregions based on clients
            var subRegionNullInMappingTable = securityGroupMappingList
               .Any(sgm => sgm.securityGroupId == securityGroupId && ((sgm.regionId == regionId && sgm.subRegionId == null) || (sgm.regionId == null && sgm.subRegionId == null)));

            //Return all subRegion
            if (subRegionNullInMappingTable)
            {
                subRegionList = getDataList
                      .Where(srg => srg.region_ident == regionId)
                      .Select(srg => new DsSubRegionResponseDto
                      {
                          subsubregion_code = srg.subsubregion_code,
                          subsubregion = srg.subsubregion
                      })
                      .DistinctBy(srg => srg.subsubregion_code)
                            .ToList();
            }
            else
            {
                var findSubRegionDetails = securityGroupMappingList
                            .Where(sgm => sgm.securityGroupId == securityGroupId && sgm.regionId == regionId && sgm.subRegionId != null)
                            .ToList();
                if (findSubRegionDetails.Any())  // Check if list is not empty
                {
                    //var regionIds = findSubRegionDetails.Select(sgm => sgm.regionId).ToHashSet();  // Optimized lookup
                    var subRegionIds = findSubRegionDetails.Select(sgm => sgm.subRegionId).ToHashSet();

                    subRegionList = getDataList
                        .Where(srg => srg.region_ident == regionId && subRegionIds.Contains(srg.subsubregion_code))
                        .Select(srg => new DsSubRegionResponseDto
                        {
                            subsubregion_code = srg.subsubregion_code,
                            subsubregion = srg.subsubregion
                        })
                        .DistinctBy(srg => srg.subsubregion_code)  // Ensure unique records
                        .ToList();
                }
            }

           




            return new APIResponse<IEnumerable<DsSubRegionResponseDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = subRegionList.OrderBy(srg => srg.subsubregion)
            };
        }


        public async Task<APIResponse<IEnumerable<DsClientResponseDto>>> GetClient()
        {
            string errorMessage = "";
            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
            int? regionId = Convert.ToInt32(_headerService.GetHeaderValue("regionId"));
            string subregionCode = _headerService.GetHeaderValue("subregionCode");

            if (string.IsNullOrWhiteSpace(securityGroupId))
            {
                errorMessage = "GetClient Error:Not found security group in Header";
                _logger.LogError(errorMessage);
                return new APIResponse<IEnumerable<DsClientResponseDto>>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = new List<DsClientResponseDto>()//For empty array
                };
            }
            if (regionId == null)
            {
                errorMessage = "GetClient Error:regionId is null";
                _logger.LogError(errorMessage);
                return new APIResponse<IEnumerable<DsClientResponseDto>>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = new List<DsClientResponseDto>()
                };
            }

            if (string.IsNullOrWhiteSpace(subregionCode))
            {
                errorMessage = "GetClient Error:subregion Code is null";
                _logger.LogError(errorMessage);
                return new APIResponse<IEnumerable<DsClientResponseDto>>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = new List<DsClientResponseDto>()
                };
            }
            // Fetch data from cache
            var response = await FillDataInCache();
            if (response.ResponseCode != 200 || response.Result == null)
            {
                errorMessage = "GetClient Error:Not found filled data in cache";
                _logger.LogError(errorMessage);
                return new APIResponse<IEnumerable<DsClientResponseDto>>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = new List<DsClientResponseDto>()
                };
            }

            var getDataList = response.Result.GetDataList;
            var securityGroupMappingList = response.Result.SecurityGroupMappingsList;
            IEnumerable<DsClientResponseDto> clients = new List<DsClientResponseDto>();

            //Check Security group Id exist in mapping table or not 
            if (!securityGroupMappingList.Any(sg => sg.securityGroupId == securityGroupId))
            {
                errorMessage = $"GetClient Error:Not found SecurityGroupId:{securityGroupId} in mapping cache";
                _logger.LogError(errorMessage);
                return new APIResponse<IEnumerable<DsClientResponseDto>>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = new List<DsClientResponseDto>()
                };
            }

            
            var subRegionNullInMappingTable = securityGroupMappingList
               .Any(sgm => sgm.securityGroupId == securityGroupId && (
               (sgm.regionId == regionId && sgm.subRegionId == subregionCode && sgm.clientId == null) ||
               (sgm.regionId == regionId && sgm.subRegionId == null && sgm.clientId == null) ||
                (sgm.regionId == null && sgm.subRegionId == null && sgm.clientId == null)
               ));

            //Return all subRegion
            if (subRegionNullInMappingTable)
            {
                clients = getDataList
                      .Where(srg => srg.region_ident == regionId && srg.subsubregion_code == subregionCode)
                      .Select(srg => new DsClientResponseDto
                      {
                          client_ident = srg.client_ident,
                          client_full_name = srg.client_full_name
                      })
                      .DistinctBy(srg => srg.client_ident)
                            .ToList();
            }
            else
            {
                var findSubRegionDetails = securityGroupMappingList
                             .Where(sgm => sgm.securityGroupId == securityGroupId && sgm.regionId == regionId && sgm.subRegionId == subregionCode && sgm.clientId != null)
                             .ToList();
                if (findSubRegionDetails.Any())  // Check if list is not empty
                {
                    //var regionIds = findSubRegionDetails.Select(sgm => sgm.regionId).ToHashSet();  // Optimized lookup
                    //var subRegionIds = findSubRegionDetails.Select(sgm => sgm.subRegionId).ToHashSet();
                    var clientIds = findSubRegionDetails.Select(sgm => sgm.clientId).ToHashSet();

                    clients = getDataList
                        .Where(srg => srg.region_ident == regionId && srg.subsubregion_code == subregionCode && clientIds.Contains(srg.client_ident))
                        .Select(srg => new DsClientResponseDto
                        {
                            client_ident = srg.client_ident,
                            client_full_name = srg.client_full_name
                        })
                        .DistinctBy(srg => srg.client_ident)  // Ensure unique records
                        .ToList();
                }
               
            }

            

            return new APIResponse<IEnumerable<DsClientResponseDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = clients.OrderBy(c => c.client_full_name)
            };
        }



        public async Task<APIResponse<FillDataCacheResponse>> FillDataInCache()
        {
            FillDataCacheResponse dataResponse = new FillDataCacheResponse();

            string cachedSecurityGroupDataKey = $"sgm_69eda_679_4bea";
            if (!_cache.TryGetValue(cachedSecurityGroupDataKey, out List<SecurityGroupMapping>? securityGroupMappingData))
            {
                _cache.Remove(cachedSecurityGroupDataKey);
                securityGroupMappingData = await GetSecurityGroupMappingsAsync();
                // Set cache options
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(60),
                    SlidingExpiration = TimeSpan.FromDays(1)
                };
                // Save data in cache
                _cache.Set(cachedSecurityGroupDataKey, securityGroupMappingData, cacheEntryOptions);
            }


            string cachedDataSliceGetDataKey = $"dsapidata_69eda_679_4bea";
            if (!_cache.TryGetValue(cachedDataSliceGetDataKey, out List<DataSliceAPIDataDto>? filterData))
            {
                _cache.Remove(cachedDataSliceGetDataKey);
                filterData = await GetDataFromDataSourceAsync();
                // Set cache options
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(60),
                    SlidingExpiration = TimeSpan.FromDays(1)
                };
                // Save data in cache
                _cache.Set(cachedDataSliceGetDataKey, filterData, cacheEntryOptions);
            }

            dataResponse.SecurityGroupMappingsList = securityGroupMappingData;
            dataResponse.GetDataList = filterData;

            return await Task.FromResult(new APIResponse<FillDataCacheResponse>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { $"Request has been completed successfully" },
                Result = dataResponse
            });
            
            
        }

       



        private async  Task<string> GenerateToken(string apiUrl, string clientId, string clientSecret, string apiVersion)
        {
           

            string token = string.Empty;
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri(apiUrl);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Add("ClientId", clientId);
                    client.DefaultRequestHeaders.Add("ClientSecret", clientSecret);
                    client.DefaultRequestHeaders.Add("x-tpds-api-version", apiVersion);
                    var stringContent = new StringContent("", Encoding.UTF8, "application/json");
                    HttpContent contentPOST = stringContent;
                    contentPOST.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    var response = await client.PostAsync("api/Account/authenticate",null);//, contentPOST);
                    //var response = await client.PostAsync("api/Authenticate/authenticate", null);//, contentPOST);
                    // Check if the request was successful
                    if (response.IsSuccessStatusCode)
                    {
                        var res = await response.Content.ReadAsStringAsync();
                        var securityToken = JsonConvert.DeserializeObject<DataSliceAPITokenResponseDto?>(res);
                        if (securityToken != null && securityToken.responseCode == 200)
                        {
                            token = securityToken.Result.Token;                          
                            
                        }
                    }
                    else
                    {

                        var res = await response.Content.ReadAsStringAsync();
                        string msg = res.ToString();
                        string message = $"DataSliceAPI: /api/Account/autheticate error: Response code - {(int)response.StatusCode}, Reason Pharase  {response.ReasonPhrase} error: {msg}";                      
                        _logger.LogError(message);



                    }

                }
            }
            catch (Exception ex)
            {
                string message = $"DataSliceAPI: /api/Account/autheticate error: {ex.Message.ToString()}";
                _logger.LogError(message);

            }
            return token;
        }


        private async  Task<List<DataSliceAPIDataDto>> GetDataFromDataSourceAsync()
        {
            //TODO: Need to be used below values in key vault 
            string apiURL = KeyVault.GetKeyVaultValue("TPDataIngestionDataSliceAPIURL").Result;
            string clientId = KeyVault.GetKeyVaultValue("TPDataIngestionDataSliceClientId").Result;
            string clientSecret = KeyVault.GetKeyVaultValue("TPDataIngestionDataSliceClientSecret").Result;
            string apiVersion = KeyVault.GetKeyVaultValue("TPDataIngestionDataSliceAPIVersion").Result; 
            string authAPIVersion = KeyVault.GetKeyVaultValue("TPDataIngestionDataSliceAuthAPIVersion").Result; 
            string sourceDataObjectId = KeyVault.GetKeyVaultValue("TPDataIngestionDataSliceSourceDataObject").Result;

         



            DataSliceGetDataResponseDto getDataResponseDto = null;
             List<DataSliceAPIDataDto> dataSliceAPIDataResult = new List<DataSliceAPIDataDto>();
            int pageSize = 5000;
            int pageNo = 1;
            int pageCount = 0;

            var ret = false;

           

            try
            {
                var bearerToken = await GenerateToken(apiURL, clientId, clientSecret, authAPIVersion);
                if (string.IsNullOrWhiteSpace(bearerToken))
                {
                    return dataSliceAPIDataResult;
                }

                do
                {
                    var dataSliceGetDataRequestDto = new DataSliceGetDataRequestDto
                    {
                        consumerApplicationId = clientId,
                        filter = "",
                        pageNo = pageNo,
                        pageSize = pageSize,
                        sourceDataObjId = sourceDataObjectId

                    };
                    using (HttpClient client = new HttpClient())
                    {

                        var data = JsonConvert.SerializeObject(dataSliceGetDataRequestDto);
                        var content = new StringContent(data, Encoding.UTF8, "application/json");
                        HttpContent contentPOST = content;
                        client.Timeout = TimeSpan.FromMinutes(5);
                        client.BaseAddress = new Uri(apiURL);
                        client.DefaultRequestHeaders.Accept.Clear();
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        client.DefaultRequestHeaders.Add("x-tpds-api-version", apiVersion);
                        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + bearerToken);
                        contentPOST.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                        var response = await client.PostAsync($"api/DataSlice/GetData", contentPOST);
                        if (response.IsSuccessStatusCode)
                        {
                            var res = await response.Content.ReadAsStringAsync();
                            getDataResponseDto = JsonConvert.DeserializeObject<DataSliceGetDataResponseDto?>(res);
                            pageCount = getDataResponseDto.result.pagination.PageCount;
                            dataSliceAPIDataResult.AddRange(getDataResponseDto.result.data);
                            ret = true;
                        }
                        else
                        {
                            var res = await response.Content.ReadAsStringAsync();
                            string msg = res.ToString();
                            string message = $"DataSliceAPI: /api/DataSlice/GetData error: Response code - {(int)response.StatusCode}, Reason Phrase  {response.ReasonPhrase} error: {msg}";
                            _logger.LogError(message);

                        }
                        pageNo++;

                    }
                } while (pageNo <= pageCount);
              
            
            }
            catch (Exception ex)
            {
                string message = $"DataSliceAPI: /api/DataSlice/GetData error: {ex.Message.ToString()}";
                _logger.LogError(message);

            }
            return dataSliceAPIDataResult;
        }
        public async Task<List<SecurityGroupMapping>> GetSecurityGroupMappingsAsync()
        {
            try
            {
                var response = await _processConfigRepositoryV3.GetSecurityGroupMappingsAsync();
                return response.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<APIResponse<string>> ClearCache()
        {
            _cache.Remove("dsapidata_69eda_679_4bea");
            _cache.Remove("sgm_69eda_679_4bea");
            _cache.Remove("chachedFilterData");
            _cache.Remove("chachedEmailTemplateData");
           // _cache.Remove("chachedDataBricksStages_734e23");
           // _cache.Remove("chachedDataBricksTerminationDetails_768e12");
            return await Task.FromResult(new APIResponse<string>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Cache cleared successfully" },
                Result = null
            });



        }




    }
}
