using Microsoft.Extensions.Caching.Distributed;
using NLog;
using NLog.Web;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.Common.Crypto;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using ZstdSharp.Unsafe;

namespace Teleperformance.DataIngestion.API.Middleware
{
    public class TokenDecryptionMiddleware
    {
        private readonly RequestDelegate _next;
  

        public TokenDecryptionMiddleware(RequestDelegate next)
        {
            _next = next;
            
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Retrieve the Bearer token from the Authorization header
            var authorizationHeader = context.Request.Headers["Authorization"];
            var repo = context.RequestServices.GetRequiredService<IProcessConfigRepositoryV3>();
            if (authorizationHeader.Count > 0 && authorizationHeader[0].StartsWith("Bearer "))
            {
                var token = authorizationHeader[0].Substring("Bearer ".Length);
                string tokenEncryptionKey = KeyVault.GetKeyVaultValue("TPDataIngestionEncryptionKey").Result;
                try
                {
                    var decryptedToken = Crypto.Decrypt(token, tokenEncryptionKey);                      

                    //TODO:
                    // Parse JWT 
                    var handler = new JwtSecurityTokenHandler();
                    
                    var jwt = handler.ReadJwtToken(decryptedToken);

                    //// Check expiration
                    //if (jwt.ValidTo < DateTime.UtcNow)
                    //{
                    //    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    //    await context.Response.WriteAsync("Token expired");
                    //    return;
                    //}
                    var expClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp)?.Value;

                    // Check revocation (using IDistributedCache or DB)
                    var jti = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
                    if (!string.IsNullOrEmpty(jti))
                    {
                        //var cache = context.RequestServices.GetRequiredService<IDistributedCache>();                        
                        //var revoked = await cache.GetStringAsync($"revoked:{jti}");
                        //if (!string.IsNullOrEmpty(revoked))
                        //{
                        //    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        //    await context.Response.WriteAsync("Token revoked");
                        //    return;
                        //}
                        var isRevoked = await repo.IsRevokedAsync(jti, context.RequestAborted);
                        if (isRevoked)
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            await context.Response.WriteAsync("Token revoked");
                            return;
                        }
                    }

                    context.Request.Headers["Authorization"] = "Bearer " + decryptedToken;
                    //context.Request.Headers["Authorization"] = new AuthenticationHeaderValue("Bearer", decryptedToken);


                }
                catch (Exception ex)
                {
                    await HandleExceptions(context, ex);
                    return;
                }
            }
            // Call the next middleware in the pipeline
            await _next(context);
        }

        private static async Task HandleExceptions(HttpContext context, Exception ex)
        {
            var controllerName = context.Request.Path.Value;
            var methodName = context.Request.Method;

            var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
            var loggeduser = "TPUtilization.API";// context.User.Identity?.Name;

            logger.Error(ex, $"[{controllerName}.{methodName}] An error occurred while processing your request.   User:{loggeduser}");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.WriteAsJsonAsync(new { });

        }
    }
}
