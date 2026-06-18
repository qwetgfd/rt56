namespace Teleperformance.DataIngestion.API.Middleware
{
    public class ForbiddenHeaderMiddleware
    {
        private readonly RequestDelegate _next;

        private static readonly string[] ForbiddenHeaders =
        {
            "x-forwarded-for",
            "x-host",
            "x-original-host",
            "x-forwarded-host",
            "forwarded",
            "forwarded-for",
            "x-original-url",
            "client-ip",
            "true-client-ip"
        };

        public ForbiddenHeaderMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            foreach (var header in ForbiddenHeaders)
            {
                if (context.Request.Headers.ContainsKey(header))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync(
                        $"Forbidden request header detected: {header}"
                    );
                    return;
                }
            }

            await _next(context);
        }

    }
}
