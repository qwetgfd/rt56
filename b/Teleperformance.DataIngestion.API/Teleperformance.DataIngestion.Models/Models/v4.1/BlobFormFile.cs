using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Models.v4._1
{
    public class BlobFormFile : IFormFile
    {
        private readonly Stream _stream;
        private readonly string _fileName;
        private readonly string _name;

        public BlobFormFile(Stream stream, string name, string fileName)
        {
            _stream = stream;
            _fileName = fileName;
            _name = name;
        }

        public string ContentType => "application/octet-stream";
        public string ContentDisposition => null;
        public IHeaderDictionary Headers => null;
        public long Length => _stream.Length;
        public string Name => _name;
        public string FileName => _fileName;

        public void CopyTo(Stream target) => _stream.CopyTo(target);
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
            _stream.CopyToAsync(target, cancellationToken);

        public Stream OpenReadStream() => _stream;
    }
}
