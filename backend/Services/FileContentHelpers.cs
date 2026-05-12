namespace Sharepoint_Plugin.Services;

internal static class FileContentHelpers
{
    public static async Task<byte[]> ReadStreamToBytesAsync(Stream stream, long? maxLength)
    {
        if (maxLength.HasValue)
        {
            var buffer = new byte[maxLength.Value];
            var bytesRead = await stream.ReadAsync(buffer).ConfigureAwait(false);
            return buffer[..bytesRead];
        }

        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms).ConfigureAwait(false);
        return ms.ToArray();
    }
}
