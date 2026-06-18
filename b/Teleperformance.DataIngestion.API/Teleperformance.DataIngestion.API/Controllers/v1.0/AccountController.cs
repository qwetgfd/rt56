using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Account;
using Teleperformance.DataIngestion.Models.Entities.v1._0.Authentication;

namespace Teleperformance.DataIngestion.API.Controllers.v1._0
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("1.0")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly ILogger<AccountController> _logger;
        public AccountController(IAccountService accountService, ILogger<AccountController> logger)
        {
            _accountService = accountService;
            _logger = logger;
        }

        [Route("GetToken")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        public async Task<IActionResult> GetToken(AuthCredentials AuthCredentials)
        {
            var result = await _accountService.GetToken(AuthCredentials);
            return StatusCode(result.ResponseCode, result);

        }


        [Route("register")]
        //[ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        public async Task<IActionResult> register(ApplicationRegistrationRequestDto applicationRegistrationRequestDto)
        {
            var resut = await _accountService.RegisterApplication(applicationRegistrationRequestDto);
            return StatusCode(resut.ResponseCode, resut);
        }

        [Route("GetUserDetail")]
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> GetUserDetail(AuthCredentials authCredentials)
        {
            var result = await _accountService.GetUserDetail(authCredentials);
            return StatusCode(result.ResponseCode, result);

        }


        [Route("authenticate")]
        [HttpGet]
        public async Task<IActionResult> authenticate()
        {
            var resut = await _accountService.Authenticate();
            return StatusCode(resut.ResponseCode, resut);
        }

       


        [Route("VerifyToken")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> VerifyToken()
        {
            _logger.LogInformation("VerifyToken API called");
            var resut = await _accountService.VerifyToken();
            return StatusCode(resut.ResponseCode, resut);
        }


        [Route("GetkeyVaultValue")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet]
        public async Task<IActionResult> GetkeyVaultValue(string value)
        {
            try
            {

                string clientID = Environment.GetEnvironmentVariable("TPDataIngestionClientId") ?? "";
                string baseUri = Environment.GetEnvironmentVariable("TPDataIngestionKeyVaultUri") ?? "";
                string clientSecret = Environment.GetEnvironmentVariable("TPDataIngestionClientSecret") ?? "";
                string tenantId = Environment.GetEnvironmentVariable("TPDataIngestionTenantId") ?? "";
                var resut = await KeyVault.GetKeyVaultValue(value);
                var getValue = $"client id : {clientID}, secret: {clientSecret}, valut uri: {baseUri}, tenantid {tenantId} and keyvaultValue {resut}";
                return StatusCode(200, getValue);
            }
            catch (Exception ex)
            {

                return StatusCode(500, ex.Message.ToString());

            }
        }

    }
}
