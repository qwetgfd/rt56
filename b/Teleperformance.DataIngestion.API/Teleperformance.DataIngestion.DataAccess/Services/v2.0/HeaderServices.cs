using Microsoft.AspNetCore.Http;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;

namespace Teleperformance.DataIngestion.DataAccess.Services.v2._0
{
    public class HeaderService : IHeaderService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HeaderService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public string GetHeaderValue(string headerName)
        {
            if (_httpContextAccessor.HttpContext == null ||
                !_httpContextAccessor.HttpContext.Request.Headers.ContainsKey(headerName))
            {
                return null;
            }

            return _httpContextAccessor.HttpContext.Request.Headers[headerName];
        }
    }
}
