using NLog;
using System.Net;

namespace Teleperformance.DataIngestion.API.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private static System.Timers.Timer? internalLogcleaner;

        public ExceptionMiddleware(RequestDelegate next)
        {
            this._next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await this._next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptions(context, ex);
            }

            finally
            {
                // Below code is to delte internalLog File in 1 week:604800sec
                internalLogcleaner = new System.Timers.Timer(604800);
                if (File.Exists("TPDataIngestion-API.txt"))
                {
                    internalLogcleaner.Elapsed += (s, e) => File.Delete("TPDataIngestion-API.txt");
                }
                internalLogcleaner.Enabled = true;
            }
        }

        private static async Task HandleExceptions(HttpContext context, Exception ex)
        {
            var controllerName = context.Request.Path.Value;
            var methodName = context.Request.Method;
            var logger = NLog.LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();
            //var logger = NLog.LogManager.Setup().LoadConfigurationFromXml.LoadConfigurationFromAppSettings().GetCurrentClassLogger();
            var loggeduser = "TPDataIngestion.API";// context.User.Identity?.Name;

            logger.Error(ex, $"[{controllerName}.{methodName}] An error occurred while processing your request.   User:{loggeduser}");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = $"An error occurred while processing your request: {ex.Message.ToString().Replace(":", ".")}" });
        }
    }
}
