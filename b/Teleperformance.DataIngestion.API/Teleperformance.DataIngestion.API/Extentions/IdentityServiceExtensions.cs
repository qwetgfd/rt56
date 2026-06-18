using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Protocols.WsFed;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;

namespace Teleperformance.DataIngestion.API.Extentions
{
    public static class IdentityServiceExtensions
    {
        public static IServiceCollection AddIdentityService(this IServiceCollection services)
        {

            string clientId = Environment.GetEnvironmentVariable("TPDataIngestionClientId");
            string clientSecret = Environment.GetEnvironmentVariable("TPDataIngestionClientSecret");
            string tenantId = Environment.GetEnvironmentVariable("TPDataIngestionTenantId");
            var key = Encoding.ASCII.GetBytes(clientSecret);



            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // Adjust this based on your deployment scenario
                    // set to true in production
                    options.RequireHttpsMetadata = true;
                    // Save the token in the server's memory
                    // 01142026 - no need to keep tokens in server memory
                    options.SaveToken = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidIssuer = clientId, // Your JWT token's issuer
                        ValidAudience = tenantId, // Your JWT token's audience
                        ClockSkew = TimeSpan.Zero // zero no grace extension
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = async ctx =>
                        {
                            var userId = ctx.Principal?.FindFirst("sub")?.Value ?? ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                            var verClaim = ctx.Principal?.FindFirst("ver")?.Value;
                            if (string.IsNullOrEmpty(verClaim))
                            {
                                ctx.Fail("Invalid token (missing sub/ver).");
                                return;
                            }
                            var repo = ctx.HttpContext.RequestServices.GetRequiredService<IAccountRepository>();
                            var userTokenVersion = await repo.GetAppUserLogin(userId);
                            if (userTokenVersion > 0)
                            {
                                if (!int.TryParse(verClaim, out var tokenVerClaim) || tokenVerClaim != userTokenVersion)
                                {
                                    ctx.Fail("Token revoked (version mismatched)");
                                    ctx.Response.Headers.Append("X-Token-Revoked", "true");
                                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                                    return;
                                }
                            }

                        }
                    };
                });

            return services;
        }
    }
}
