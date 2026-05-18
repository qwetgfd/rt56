namespace Sharepoint_Plugin.Utilities;

/// <summary>Reusable stream helpers.</summary>
public static class StreamHelpers
{
    public static async Task<byte[]> ToBytesAsync(this Stream stream)
    {
        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms).ConfigureAwait(false);
        return ms.ToArray();
    }
}
