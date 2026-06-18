namespace Teleperformance.DataIngestion.API.Middleware
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }
        public async Task Invoke(HttpContext context)
        {
            // Content Security Policy (CSP) header
            context.Response.Headers.Add("Content-Security-Policy", "default-src 'self'");

            // HTTP Strict Transport Security (HSTS) header
            //context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000");

            // X-Content-Type-Options header
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");

            // X-Frame-Options header
            context.Response.Headers.Add("X-Frame-Options", "DENY");

            // X-XSS-Protection header
            context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");

            // Referrer-Policy header
            context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");

            // Feature-Policy header
            context.Response.Headers.Add("Feature-Policy", "geolocation 'self'; microphone 'none'; camera 'none'");

            // Cache-Control header
            context.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");

            // Expect-CT header
            context.Response.Headers.Add("Expect-CT", "enforce, max-age=30");

            // Permissions-Policy header
            context.Response.Headers.Add("Permissions-Policy", "geolocation=(), microphone=(), camera=()");

            //Remove Server Version Disclosure
            context.Response.Headers.Remove("Server");

            context.Response.Headers.Remove("X-Powered-By");

            context.Response.Headers.Remove("X-AspNet-Version");

            //context.Response.Headers.Remove("Server");

            context.Response.Headers["Server"] = "CustomServer";

            await _next(context);
        }
    }
}
