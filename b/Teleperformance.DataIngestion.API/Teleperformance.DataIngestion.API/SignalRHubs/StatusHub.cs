using Microsoft.AspNetCore.SignalR;

namespace Teleperformance.DataIngestion.API.SignalRHubs
{
    public class StatusHub : Hub
    {
        public async Task SendSatus(string status)
        {
            await Clients.Caller.SendAsync("ReceiveGenerateEIBStatus", status);
        }

        public async Task SendPDMProflingSatusBySPId(int spId)
        {
            await Clients.Caller.SendAsync("SendPDMProflingSatusBySPId", spId);
        }
    }
}
