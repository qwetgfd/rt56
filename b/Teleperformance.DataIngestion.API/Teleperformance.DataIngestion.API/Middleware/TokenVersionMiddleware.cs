using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Teleperformance.DataIngestion.Common;

namespace Teleperformance.DataIngestion.API.Middleware
{
    public class TokenVersionMiddleware
    {
        private readonly RequestDelegate _next;

        public TokenVersionMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var authorizationHeader = context.Request.Headers["Authorization"];

            // Only check when user is authenticated
            if (authorizationHeader.Count > 0 && authorizationHeader[0].StartsWith("Bearer "))
            {
                var token = authorizationHeader[0].Substring("Bearer ".Length);
                string tokenEncryptionKey = KeyVault.GetKeyVaultValue("TPDataIngestionEncryptionKey").Result;

                try
                {

                }
                catch
                {

                }
            }

            await _next(context);
        }
    }

}
