using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.Net;

namespace Teleperformance.DataIngestion.Common
{
    public static class KeyVault
    {
        public static async Task<string> GetKeyVaultValue(string KeyVaultName)
        {
            try
            {
                string clientID = Environment.GetEnvironmentVariable("TPDataIngestionClientId") ?? "";
                string baseUri = Environment.GetEnvironmentVariable("TPDataIngestionKeyVaultUri") ?? "";
                string clientSecret = Environment.GetEnvironmentVariable("TPDataIngestionClientSecret") ?? "";
                string tenantId = Environment.GetEnvironmentVariable("TPDataIngestionTenantId") ?? "";
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13;
                ClientSecretCredentialOptions clientSecretCredentialOptions = new ClientSecretCredentialOptions();
                var clientsecretcred = new ClientSecretCredential(tenantId, clientID, clientSecret,
                    clientSecretCredentialOptions);
                var client = new SecretClient(new Uri(baseUri), clientsecretcred);
                var secretData = await client.GetSecretAsync(KeyVaultName);
                return secretData.Value.Value.ToString();
            }
            catch (Exception ex)
            {

                throw new Exception(ex.Message.ToString());
            }
        }
    }
}
