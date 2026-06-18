using Microsoft.AspNetCore.Http;
namespace Teleperformance.DataIngestion.Models.Models.v4._1
{
    /// <summary>
    /// Lightweight IFormFile used only to pass the FileName into your existing validators.
    /// It returns an empty stream because validation does not read file content.
    /// </summary>
    public sealed class SmbFormFile : IFormFile
    {
        private static readonly Stream _empty = new MemoryStream(Array.Empty<byte>());

        public SmbFormFile(string fileName, string name = null)
        {
            FileName = fileName;
            Name = name ?? fileName;
        }

        public string ContentType => "application/octet-stream";
        public string ContentDisposition => null;
        public IHeaderDictionary Headers => null;
        public long Length => 0;
        public string Name { get; }
        public string FileName { get; }

        public void CopyTo(Stream target) { /* no-op */ }
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Stream OpenReadStream() => _empty; // validators only need FileName
    }
}
