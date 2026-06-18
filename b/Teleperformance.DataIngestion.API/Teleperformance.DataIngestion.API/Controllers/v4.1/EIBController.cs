using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NLog.Layouts;
using NPOI.SS.UserModel;
using System.IO;
using Teleperformance.DataIngestion.API.SignalRHubs;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.EIB;
using Teleperformance.DataIngestion.Models.Entities.v4._1.EIB;

namespace Teleperformance.DataIngestion.API.Controllers.v4._1
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("4.1")]
    [Authorize]
    public class EIBController : ControllerBase
    {
        private readonly ILogger<EIBController> logger;
        private readonly IEIBServiceV4_1 EIBService;
        private readonly IHubContext<StatusHub> hubContext;

        public EIBController(ILogger<EIBController> logger,
            IEIBServiceV4_1 EIBService,
            IHubContext<StatusHub> hubContext
            )
        {
            this.logger = logger;
            this.EIBService = EIBService;
            this.hubContext = hubContext;
        }

        [Route("GetEIBRequiredBPKeyword")]
        [HttpGet]
        public async Task<IActionResult> GetEIBRequiredBPKeyword()
        {
            var result = await EIBService.GetEIBRequiredBPKeyword();
            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetAllViews")]
        [HttpGet]
        public async Task<IActionResult> GetAllViews()
        {
            var result = await EIBService.GetAllViews();
            return StatusCode(result.ResponseCode, result);
        }

        

        [Route("InsertEIBDetails")]
        [HttpPost, RequestSizeLimit(30 * 1024 * 1024)]
        public async Task<IActionResult> InsertEIBDetails(bool e)
        {
            try
            {
                var formCollection = await Request.ReadFormAsync();
                IFormFile? file = null;
                Stream? stream = null;
                if (e)
                {
                    if (formCollection.Files.Count() != 2)
                    {
                        return BadRequest();
                    }
                    file = formCollection.Files.First();
                    stream = file.OpenReadStream();
                }
                var loggedInUser = formCollection["LoggedInUser"].ToString();
                var userName = formCollection["UserName"].ToString();
                string json = string.Empty;
                using (var sr = new StreamReader(formCollection.Files[e ? 1 : 0].OpenReadStream()))
                {
                    json = await sr.ReadToEndAsync();
                }


                var result = await EIBService.InsertEIBDetails(json, e ? file : null, e ? stream : null, userName, loggedInUser);

                return StatusCode(result.ResponseCode, result);
            }
            catch (Exception ex)
            {
                logger.LogError("Error in InsertEIBDetails " + ex.Message, ex.InnerException);
                throw;
            }

        }

        [Route("GetAllEIB")]
        [HttpGet]
        public async Task<IActionResult> GetAllEIB([FromQuery] EIBListRequestDto request)
        {
            var result = await EIBService.GetAllEIBs(request);
            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetEIBCountries")]
        [HttpGet]
        public async Task<IActionResult> GetCountries()
        {
            var result = await EIBService.GetCountries();
            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetEIBByEIBId")]
        [HttpGet]
        public async Task<IActionResult> GetEIBByEIBId(string eibId)
        {
            var result = await EIBService.GetEIBByEIBId(eibId);
            return StatusCode(result.ResponseCode, result);
        }

        [Route("CheckActiveEIBConfiguration")]
        [HttpGet]
        public async Task<IActionResult> CheckActiveEIBConfiguration(string EIBName)
        {
            var result = await EIBService.CheckActiveEIBConfiguration(EIBName);
            return StatusCode(result.ResponseCode, result);
        }

        [Route("GenerateEIB")]
        [HttpPost]
        public async Task<IActionResult> GenerateEIB(string EIBId)
        {
            var result = await EIBService.GenerateEIB(EIBId);
            return StatusCode(result.ResponseCode, result);
        }

        [HttpGet("getEIBGenerationStatus")]
        public async Task<IActionResult> getEIBGenerationStatus(DateTime generationStartDateTime, DateTime generationEndDateTime, CancellationToken cancellationToken)
        {
            bool continueGettingStatus = true;
            do
            {
                var result = await EIBService.getEIBGenerationStatus(generationStartDateTime, generationEndDateTime, cancellationToken);
                if (result.ResponseCode == 200) {
                    await hubContext.Clients.All.SendAsync("ReceiveGenerateEIBStatus", result.Result);                    
                }
                await Task.Delay(1000);
            } while (continueGettingStatus);
            return Ok("Process completed");
            //for (int i = 0; i <= 100; i += 10)
            //{
            //    await hubContext.Clients.All.SendAsync("ReceiveGenerateEIBStatus", $"Progress: {i}%");
            //    await Task.Delay(1000); // simulate work
            //}
            //return Ok("Process completed");
        }

        [Route("GetAllProfilingSP")]
        [HttpGet]
        public async Task<IActionResult> GetAllProfilingSP()
        {
            var result = await EIBService.GetAllProfilingSP();
            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetCurrentStatusOfPDMSP")]
        [HttpGet]
        public async Task<IActionResult> GetCurrentStatusOfPDMSP(int procedureNameId)
        {
            var result = await EIBService.GetCurrentStatusOfPDMSP(procedureNameId);
            return StatusCode(result.ResponseCode, result);
        }

        [HttpGet("SendPDMProflingSatusBySPId")]
        public async Task<IActionResult> SendPDMProflingSatusBySPId(int procedureNameId, string runId, string connectionId, CancellationToken cancellationToken, int lastSeendId = 0)
        {
            #region old code
            //bool continueGettingStatus = true;
            //int currentSkip = 0;
            //List<ProfilingLogs> logBuffer = new List<ProfilingLogs>();
            //do
            //{
            //    Console.WriteLine($"currentSkip:${currentSkip}");
            //    // 1. Get whatever is available since our last skip
            //    var result = await EIBService.SendPDMProflingSatusBySPId(procedureNameId, runId, cancellationToken, currentSkip);

            //    if (result.ResponseCode == 200 && result.Result.Any())
            //    {
            //        var newLogs = result.Result.ToList();
            //        // 2. Add found rows to our buffer and update the database pointer
            //        if (newLogs.Last().Id > 0 || (newLogs.Last().runningStatus?.Trim() == "ended" || newLogs.Last().runningStatus?.Trim() == "failed"))
            //        {
            //            logBuffer.AddRange(newLogs);
            //            currentSkip = newLogs.Last().Id;
            //        }
            //    }

            //    // 3. While we have 10 or more, keep sending "Full Batches"
            //    while (logBuffer.Count >= 10)
            //    {
            //        var batchToSend = logBuffer.Take(10).ToList();
            //        await hubContext.Clients.All.SendAsync("SendPDMProflingSatusBySPId", batchToSend);

            //        // Remove the 10 we just sent from the buffer
            //        logBuffer.RemoveRange(0, 10);

            //        // Check if any of the sent rows indicate the process is over
            //        if (batchToSend.Any(l => l.runningStatus?.ToLower() == "ended" || l.runningStatus?.ToLower() == "failed"))
            //        {
            //            continueGettingStatus = false;
            //            break;
            //        }
            //    }

            //    // 4. Handle the "End of Process" partial batch
            //    // If the process ended but we have 1-9 rows left in buffer, send them now.
            //    if (continueGettingStatus == true)
            //    {
            //        // Check if the most recently fetched logs (still in buffer) say it's ended
            //        if (logBuffer.Any(l => l.runningStatus?.ToLower() == "ended" || l.runningStatus?.ToLower() == "failed"))
            //        {
            //            await hubContext.Clients.All.SendAsync("SendPDMProflingSatusBySPId", logBuffer);
            //            logBuffer.Clear();
            //            continueGettingStatus = false;
            //        }
            //    }

            //    if (continueGettingStatus)
            //    {
            //        await Task.Delay(1000, cancellationToken);
            //    }

            //} while (continueGettingStatus);

            //return Ok("Process completed");
            #endregion

            #region test
            bool continueGettingStatus = true;
            int currentSkip = lastSeendId;
            // Use a queue for FIFO behavior (send one at a time in correct order)
            var logBuffer = new Queue<ProfilingLogs>();

            do
            {
                Console.WriteLine($"currentSkip: {currentSkip}");

                // 1) Get whatever is available since our last skip
                var result = await EIBService.SendPDMProflingSatusBySPId(
                    procedureNameId,
                    runId,
                    cancellationToken,
                    currentSkip
                );

                if (result.ResponseCode == 200 && result.Result?.Any() == true)
                {

                    // Ensure ascending order by Id before enqueueing (in case service returns unordered)
                    var newLogs = result.Result.OrderBy(l => l.Id).ToList();
                    if (newLogs.Last().Id > 0 || (newLogs.Last().runningStatus?.Trim() == "ended" || newLogs.Last().runningStatus?.Trim() == "failed"))
                    {
                        foreach (var log in newLogs)
                        {
                            logBuffer.Enqueue(log);
                        }

                        // Advance the pointer to the latest item received
                        currentSkip = newLogs.Last().Id;
                    }
                }

                // 2) While we have anything in buffer, send strictly one item per SignalR call
                while (logBuffer.Count > 0 && !cancellationToken.IsCancellationRequested)
                {
                    var nextLog = logBuffer.Dequeue();

                    // --- Option A (recommended for compatibility): send as a single-item array ---
                    // If your Angular handler expects a list/array, this keeps the payload shape the same.
                    await hubContext.Clients.Clients(connectionId).SendAsync(
                        "SendPDMProflingSatusBySPId",
                        new[] { nextLog }, // single-item array
                        cancellationToken
                    );

                    // --- Option B (if your client expects a single object): ---
                    // await hubContext.Clients.All.SendAsync(
                    //     "SendPDMProflingSatusBySPId",
                    //     nextLog,
                    //     cancellationToken
                    // );

                    // Stop immediately if the process is ended/failed
                    var status = nextLog.runningStatus?.Trim();
                    if (string.Equals(status, "ended", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                    {
                        continueGettingStatus = false;
                        break;
                    }
                }

                // 3) If nothing to send, wait a bit before polling again
                if (continueGettingStatus && logBuffer.Count == 0 && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, cancellationToken);
                }

            } while (continueGettingStatus && !cancellationToken.IsCancellationRequested);

            return Ok("Process completed");
            #endregion

        }

        [HttpPost("RegisterPDMRProfilingSPRun")]
        public async Task<IActionResult> RegisterPDMRProfilingSPRun([FromBody] PDMProfilingRequestDto request, CancellationToken ct)
        {
            var result = await EIBService.RegisterPDMRProfilingSPRun(request.ProcedureNameId, request.ProcessedBy, ct);
            return StatusCode(result.ResponseCode, result);
        }

        static bool IsTerminal(ProfilingLogs l)
        {
            var status = l?.runningStatus?.Trim();
            return status != null &&
                   (status.Equals("ended", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("failed", StringComparison.OrdinalIgnoreCase));
        }

        static string BuildGroupName(int procedureNameId, string runId)
            => $"PDM::{procedureNameId}::{runId}";
    }
}
