using Renci.SshNet;
using Renci.SshNet.Sftp;
using Renci.SshNet.Async; // async extension methods
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.DataAccess.Helpers
{
    public sealed class SftpHelper : IDisposable
    {
        private readonly SftpClient _client;

        // ---- Constructors ----

        public SftpHelper(string host, int port, string username, string password)
        {
            _client = new SftpClient(host, port, username, password);
        }

        public SftpHelper(string host, int port, string username, string privateKeyPath, string passphrase = null)
        {
            var keyFile = string.IsNullOrWhiteSpace(passphrase)
                ? new PrivateKeyFile(privateKeyPath)
                : new PrivateKeyFile(privateKeyPath, passphrase);

            var connectionInfo = new ConnectionInfo(
                host, port, username,
                new PrivateKeyAuthenticationMethod(username, keyFile)
            );

            _client = new SftpClient(connectionInfo);
        }

        // ---- Connection mgmt ----
        private void EnsureConnected()
        {
            if (!_client.IsConnected)
                _client.Connect();
        }

        private async Task EnsureConnectedAsync(CancellationToken ct = default)
        {
            if (!_client.IsConnected)
                await _client.ConnectAsync(ct).ConfigureAwait(false);
        }

        public bool IsConnected => _client?.IsConnected == true;

        // ---- SYNC listing helper ----
        public IReadOnlyList<ISftpFile> ListFiles(
            string remoteDirectory,
            string searchPattern = null,
            bool excludeHiddenDotFiles = true)
        {
            EnsureConnected();

            var entries = _client.ListDirectory(remoteDirectory);

            var files = entries
                .Where(f => !f.IsDirectory)
                .Where(f => !(excludeHiddenDotFiles && f.Name.StartsWith(".", StringComparison.Ordinal)))
                .Cast<ISftpFile>()
                .ToList();

            if (!string.IsNullOrWhiteSpace(searchPattern))
            {
                string pattern = searchPattern.Trim();
                bool startsWithWildcard = pattern.StartsWith("*", StringComparison.Ordinal);
                bool endsWithWildcard = pattern.EndsWith("*", StringComparison.Ordinal);
                string core = pattern.Trim('*');

                files = files.Where(f =>
                {
                    if (startsWithWildcard && endsWithWildcard)
                        return f.Name.Contains(core, StringComparison.OrdinalIgnoreCase);
                    if (startsWithWildcard)
                        return f.Name.EndsWith(core, StringComparison.OrdinalIgnoreCase);
                    if (endsWithWildcard)
                        return f.Name.StartsWith(core, StringComparison.OrdinalIgnoreCase);
                    return f.Name.Equals(core, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }

            return files;
        }

        // If you still want a SYNC "AnyFileExists" for legacy callers, keep this:
        public bool AnyFileExists(string remoteDirectory, string searchPattern = null, bool excludeHiddenDotFiles = true)
        {
            var files = ListFiles(remoteDirectory, searchPattern, excludeHiddenDotFiles);
            return files.Count > 0;
        }

        // ---- ASYNC listing helper ----
        public async Task<IReadOnlyList<ISftpFile>> ListFilesAsync(
            string remoteDirectory,
            string searchPattern = null,
            bool excludeHiddenDotFiles = true,
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

            var allEntries = new List<ISftpFile>();

            // Enumerate the IAsyncEnumerable<ISftpFile>
            await foreach (var entry in _client
                .ListDirectoryAsync(remoteDirectory, cancellationToken)
                .WithCancellation(cancellationToken))
            {
                allEntries.Add(entry);
            }

            var files = allEntries
                .Where(f => !f.IsDirectory)
                .Where(f => !(excludeHiddenDotFiles && f.Name.StartsWith(".", StringComparison.Ordinal)))
                .ToList();

            if (!string.IsNullOrWhiteSpace(searchPattern))
            {
                string pattern = searchPattern.Trim();
                bool startsWithWildcard = pattern.StartsWith("*", StringComparison.Ordinal);
                bool endsWithWildcard = pattern.EndsWith("*", StringComparison.Ordinal);
                string core = pattern.Trim('*');

                files = files.Where(f =>
                {
                    if (startsWithWildcard && endsWithWildcard)
                        return f.Name.Contains(core, StringComparison.OrdinalIgnoreCase);
                    if (startsWithWildcard)
                        return f.Name.EndsWith(core, StringComparison.OrdinalIgnoreCase);
                    if (endsWithWildcard)
                        return f.Name.StartsWith(core, StringComparison.OrdinalIgnoreCase);
                    return f.Name.Equals(core, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }

            return files;
        }

        public async Task<bool> AnyFileExistsAsync(
            string remoteDirectory,
            string searchPattern = null,
            bool excludeHiddenDotFiles = true,
            CancellationToken cancellationToken = default)
        {
            var files = await ListFilesAsync(remoteDirectory, searchPattern, excludeHiddenDotFiles, cancellationToken)
                                .ConfigureAwait(false);
            return files.Count > 0;
        }

        public ISftpFile GetLatestFile(string remoteDirectory, string searchPattern = null, bool excludeHiddenDotFiles = true)
        {
            var files = ListFiles(remoteDirectory, searchPattern, excludeHiddenDotFiles);
            return files.OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
        }

        public async Task<ISftpFile> GetLatestFileAsync(
            string remoteDirectory,
            string searchPattern = null,
            bool excludeHiddenDotFiles = true,
            CancellationToken cancellationToken = default)
        {
            var files = await ListFilesAsync(remoteDirectory, searchPattern, excludeHiddenDotFiles, cancellationToken)
                                .ConfigureAwait(false);
            return files.OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
        }

        // ---- Wait helpers ----

        // Preferred async polling
        public async Task<bool> WaitForAnyFileAsync(
            string remoteDirectory,
            TimeSpan timeout,
            TimeSpan? pollInterval = null,
            string searchPattern = null,
            bool excludeHiddenDotFiles = true,
            CancellationToken cancellationToken = default)
        {
            var interval = pollInterval ?? TimeSpan.FromSeconds(2);
            var deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow <= deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await AnyFileExistsAsync(remoteDirectory, searchPattern, excludeHiddenDotFiles, cancellationToken)
                        .ConfigureAwait(false))
                    return true;

                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
            return false;
        }

        // Optional SYNC wrapper (keeps old callers working)
        public bool WaitForAnyFile(
            string remoteDirectory,
            TimeSpan timeout,
            TimeSpan? pollInterval = null,
            string searchPattern = null,
            bool excludeHiddenDotFiles = true)
        {
            return WaitForAnyFileAsync(remoteDirectory, timeout, pollInterval, searchPattern, excludeHiddenDotFiles)
                .GetAwaiter().GetResult();
        }

        // ---- Download ----

        public void DownloadFile(string remoteFilePath, string localFilePath, bool overwrite = true)
        {
            EnsureConnected();

            var localDir = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
                Directory.CreateDirectory(localDir);

            if (File.Exists(localFilePath) && !overwrite)
                throw new IOException($"File already exists: {localFilePath}");

            using var fs = File.Open(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            _client.DownloadFile(remoteFilePath, fs);
        }

        public async Task DownloadFileAsync(
            string remoteFilePath,
            string localFilePath,
            bool overwrite = true,
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

            var localDir = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
                Directory.CreateDirectory(localDir);

            if (File.Exists(localFilePath) && !overwrite)
                throw new IOException($"File already exists: {localFilePath}");

            using var fs = File.Open(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await _client.DownloadFileAsync(remoteFilePath, fs, cancellationToken).ConfigureAwait(false);
        }

        // ---- Existence helpers ----
        public async Task<bool> FileExistsAsync(string remoteFilePath, CancellationToken ct = default)
        {
            await EnsureConnectedAsync(ct).ConfigureAwait(false);

            var exists = await _client.ExistsAsync(remoteFilePath, ct).ConfigureAwait(false);
            if (!exists) return false;

            var attrs = await _client.GetAttributesAsync(remoteFilePath, ct).ConfigureAwait(false);
            return !attrs.IsDirectory;
        }

        public async Task<bool> DirectoryExistsAsync(string remoteDirectoryPath, CancellationToken ct = default)
        {
            await EnsureConnectedAsync(ct).ConfigureAwait(false);

            var exists = await _client.ExistsAsync(remoteDirectoryPath, ct).ConfigureAwait(false);
            if (!exists) return false;

            var attrs = await _client.GetAttributesAsync(remoteDirectoryPath, ct).ConfigureAwait(false);
            return attrs.IsDirectory;
        }

        public void Dispose()
        {
            try
            {
                if (_client?.IsConnected == true)
                    _client.Disconnect();
            }
            finally
            {
                _client?.Dispose();
            }
        }
    }
}