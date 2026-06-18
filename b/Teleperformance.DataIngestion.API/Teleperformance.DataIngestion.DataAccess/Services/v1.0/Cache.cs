using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v2._0.EmailNotification;
using Teleperformance.DataIngestion.Models.Entities.v4._0.DataBricksAPIJobStatus;

namespace Teleperformance.DataIngestion.DataAccess.Services.v1._0
{
    public class Cache:ICache
    {
        private readonly ILogger<Cache> _logger;
        private readonly IDapperService _dapperService;
        private readonly IMemoryCache _cache;
        private readonly IProcessConfigurationRepository _iProcessConfigurationRepository;
        private readonly IEmailNotificationRepository _iEmailNotificationRepository;
        private readonly IDatabricksAPIDbRepository _iDatabricksAPIDbRepository;


        public Cache(ILogger<Cache> logger, IDapperService dapperService, IMemoryCache cache, IProcessConfigurationRepository iProcessConfigurationRepository, IEmailNotificationRepository iEmailNotificationRepository, IDatabricksAPIDbRepository iDatabricksAPIDbRepository)
        {
            this._logger = logger;
            this._dapperService = dapperService;
            this._cache = cache;
            this._iProcessConfigurationRepository = iProcessConfigurationRepository;
            _iEmailNotificationRepository = iEmailNotificationRepository;
            _iDatabricksAPIDbRepository = iDatabricksAPIDbRepository;
        }
        public async Task<IEnumerable<DateTimeFormats>?> GetFormatListAsync()
        {
            string cachedFilterDataKey = $"chachedFilterData";
            if (!_cache.TryGetValue(cachedFilterDataKey, out IEnumerable<DateTimeFormats>? filterData))
            {
                _cache.Remove(cachedFilterDataKey);
                // If not in cache, generate the data
                filterData = await _iProcessConfigurationRepository.GetDateTimeFormatList();

                // Set cache options
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(60),
                    SlidingExpiration = TimeSpan.FromDays(1)
                };

                // Save data in cache
                _cache.Set(cachedFilterDataKey, filterData, cacheEntryOptions);
            }
            return filterData;
        }



        public async Task<EmailNotificationTemplate?> GetEmailTemplateListAsync()
        {
            string cachedFilterDataKey = $"chachedEmailTemplateData";
            if (!_cache.TryGetValue(cachedFilterDataKey, out EmailNotificationTemplate? filterData))
            {
                _cache.Remove(cachedFilterDataKey);
                // If not in cache, generate the data
                filterData = await _iEmailNotificationRepository.GetEmailNotificationTemplate();
                // Set cache options
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(60),
                    SlidingExpiration = TimeSpan.FromDays(1)
                };
                // Save data in cache
                _cache.Set(cachedFilterDataKey, filterData, cacheEntryOptions);
            }
            return filterData;
        }


        public async Task<IEnumerable<DataBricksStages>?> GetDataBricksStagesAsync()
        {
            string cachedFilterDataKey = $"chachedDataBricksStages_734e23";
            if (!_cache.TryGetValue(cachedFilterDataKey, out IEnumerable<DataBricksStages>? filterData))
            {
                _cache.Remove(cachedFilterDataKey);
                // If not in cache, generate the data
                filterData = await _iDatabricksAPIDbRepository.GetDatabricksStages();

                // Set cache options
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(60),
                    SlidingExpiration = TimeSpan.FromDays(1)
                };

                // Save data in cache
                _cache.Set(cachedFilterDataKey, filterData, cacheEntryOptions);
            }
            return filterData;
        }

        public async Task<IEnumerable<DatabricksTerminationDetails>?> GetDatabricksTerminationDetailsAsync()
        {
            string cachedFilterDataKey = $"chachedDataBricksTerminationDetails_768e12";
            if (!_cache.TryGetValue(cachedFilterDataKey, out IEnumerable<DatabricksTerminationDetails>? filterData))
            {
                _cache.Remove(cachedFilterDataKey);
                // If not in cache, generate the data
                filterData = await _iDatabricksAPIDbRepository.GetDatabricksTerminationDetailsAsync();

                // Set cache options
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(60),
                    SlidingExpiration = TimeSpan.FromDays(1)
                };

                // Save data in cache
                _cache.Set(cachedFilterDataKey, filterData, cacheEntryOptions);
            }
            return filterData;
        }
        public async Task<APIResponse<string>> ClearCache()
        {

            
            string cachedFilterDataKey = $"chachedFilterData";
            _cache.Remove(cachedFilterDataKey);
            return await Task.FromResult(new APIResponse<string>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = null
            });


        }

    }
}
