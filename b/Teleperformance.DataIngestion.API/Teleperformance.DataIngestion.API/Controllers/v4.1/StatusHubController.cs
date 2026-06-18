using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Teleperformance.DataIngestion.API.SignalRHubs;

namespace Teleperformance.DataIngestion.API.Controllers.v4._1
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("4.1")]
    [Authorize]
    public class StatusHubController : ControllerBase
    {
        private readonly IHubContext<StatusHub> hubContext;

        public StatusHubController(IHubContext<StatusHub> hubContext)
        {
            this.hubContext = hubContext;
        }

        [HttpGet("start")]
        public async Task<IActionResult> StartProcess()
        {


            for (int i = 0; i <= 100; i += 10)
            {
                await hubContext.Clients.All.SendAsync("ReceiveGenerateEIBStatus", $"Progress: {i}%");
                await Task.Delay(1000); // simulate work
            }
            return Ok("Process completed");
        }
    }
}
