using Microsoft.AspNetCore.Authentication;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;


namespace Teleperformance.DataIngestion.API.Middleware
{

    public class IdleTimeoutMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly TimeSpan _idle = TimeSpan.FromMinutes(15);

        public IdleTimeoutMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext context)
        {
            var principal = context.User;
            var repo = context.RequestServices.GetRequiredService<IProcessConfigRepository>();
            if (principal?.Identity?.IsAuthenticated == true)
            {
                var lastSeen = context.Session.GetString("lastSeenUtc");
                //var temp = repo
                var now = DateTime.UtcNow;

                if (DateTime.TryParse(lastSeen, out var last))
                {
                    if (now - last > _idle)
                    {
                        await context.SignOutAsync();
                    }
                }
                context.Session.SetString("lastSeenUtc", now.ToString("o"));
            }
            await _next(context);
        }
    }

}
