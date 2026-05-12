using System.Text.Json;
using Sharepoint_Plugin.Models;

namespace Sharepoint_Plugin.Interfaces.V_1_0;

public interface ISharePointRepository
{
    Task<JsonDocument> GetJsonDocumentAsync(string url);
    Task<JsonDocument> PostJsonDocumentAsync(string url, object body);
    Task<SharePointFileStreamContent> GetContentStreamAsync(string url);
}
