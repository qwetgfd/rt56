using SMBLibrary.Client;
using SMBLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0
{
    public class SmbFileWriteStream : Stream
    {
        private readonly ISMBFileStore _fileStore;
        private readonly object _fileHandle;
        private long _position;

        public SmbFileWriteStream(ISMBFileStore fileStore, object fileHandle)
        {
            _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
            _fileHandle = fileHandle ?? throw new ArgumentNullException(nameof(fileHandle));
            _position = 0;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            // SMB doesn't require explicit flushing, so this can be left empty
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("This stream does not support reading.");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("This stream does not support seeking.");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("This stream does not support setting length.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // Step 1: Extract the part of the buffer to write
            byte[] dataToWrite = new byte[count];
            Array.Copy(buffer, offset, dataToWrite, 0, count);

            // Step 2: Perform the SMB write operation
            int bytesWritten;
            NTStatus status = _fileStore.WriteFile(out bytesWritten, _fileHandle, _position, dataToWrite);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                throw new IOException($"Failed to write to SMB file. Status: {status}");
            }

            // Step 3: Update the stream's position
            _position += bytesWritten;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Ensure that the file handle is closed when the stream is disposed
                _fileStore.CloseFile(_fileHandle);
            }

            base.Dispose(disposing);
        }
    }
}
