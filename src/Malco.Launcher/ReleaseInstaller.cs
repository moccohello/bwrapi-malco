using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Malco.Launcher
{
    internal sealed class ReleaseInstaller : IDisposable
    {
        private readonly LauncherPolicy _policy;
        private readonly ReleaseVerifier _verifier;
        private readonly InstallStateStore _stateStore;
        private readonly HttpClient _client;

        public ReleaseInstaller(
            LauncherPolicy policy,
            ReleaseVerifier verifier,
            InstallStateStore stateStore)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _client = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None
            });
        }

        public VerifiedEnvelope FetchLatest()
        {
            var bytes = DownloadBytes(
                    _policy.FeedUri,
                    _policy.MaximumEnvelopeBytes,
                    TimeSpan.FromMilliseconds(_policy.FeedTimeoutMilliseconds))
                .GetAwaiter().GetResult();
            return _verifier.VerifyEnvelope(bytes);
        }

        public ReleaseReference Install(
            VerifiedEnvelope envelope,
            IProgress<UpdateProgress> progress = null)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));
            progress?.Report(new UpdateProgress(UpdateStage.Preparing));
            var reference = new ReleaseReference(
                envelope.Manifest.Sequence,
                envelope.ManifestSha256);
            var finalDirectory = _stateStore.VersionDirectory(reference);
            if (Directory.Exists(finalDirectory))
            {
                progress?.Report(new UpdateProgress(UpdateStage.Verifying));
                _verifier.VerifyInstalledRelease(reference, finalDirectory);
                return reference;
            }

            var stagingDirectory = Path.Combine(_stateStore.StagingRoot, Guid.NewGuid().ToString("N"));
            var archivePath = Path.Combine(stagingDirectory, "payload.zip");
            var versionDirectory = Path.Combine(stagingDirectory, "version");
            var payloadDirectory = Path.Combine(versionDirectory, "payload");
            var partialRoot = Path.Combine(stagingDirectory, "partials");
            try
            {
                Directory.CreateDirectory(stagingDirectory);
                WriteDurableFile(
                    Path.Combine(stagingDirectory, ".malco-staging-v1"),
                    System.Text.Encoding.ASCII.GetBytes("malco-staging=1\r\n"));
                Directory.CreateDirectory(versionDirectory);
                WriteDurableFile(
                    Path.Combine(versionDirectory, "release-envelope.json"),
                    envelope.EnvelopeBytes);
                Directory.CreateDirectory(payloadDirectory);
                Directory.CreateDirectory(partialRoot);
                DownloadArchive(envelope.Manifest.Archive, archivePath, progress).GetAwaiter().GetResult();
                progress?.Report(new UpdateProgress(UpdateStage.Verifying));
                ExtractClosedArchive(
                    archivePath,
                    payloadDirectory,
                    partialRoot,
                    envelope.Manifest.Files);
                _verifier.VerifyPayloadTree(payloadDirectory, envelope.Manifest.Files);
                File.Delete(archivePath);
                Directory.Move(versionDirectory, finalDirectory);
                progress?.Report(new UpdateProgress(UpdateStage.Finalizing));
                _verifier.VerifyInstalledRelease(reference, finalDirectory);
                return reference;
            }
            finally
            {
                OwnedInstallCleaner.CleanStagingDirectory(stagingDirectory, _verifier);
            }
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        private async Task<byte[]> DownloadBytes(Uri uri, int maximumBytes, TimeSpan timeout)
        {
            using (var cancellation = new CancellationTokenSource(timeout))
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            using (var response = await _client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellation.Token).ConfigureAwait(false))
            {
                RequireSuccess(response);
                if (response.Content.Headers.ContentLength.HasValue &&
                    response.Content.Headers.ContentLength.Value > maximumBytes)
                {
                    throw new InvalidDataException("The HTTPS response exceeds its size limit.");
                }
                using (var input = await response.Content.ReadAsStreamAsync(cancellation.Token).ConfigureAwait(false))
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[32 * 1024];
                    while (true)
                    {
                        var read = await input.ReadAsync(buffer, cancellation.Token).ConfigureAwait(false);
                        if (read == 0) break;
                        if (output.Length > maximumBytes - read)
                        {
                            throw new InvalidDataException("The HTTPS response exceeds its size limit.");
                        }
                        output.Write(buffer, 0, read);
                    }
                    if (output.Length == 0)
                    {
                        throw new InvalidDataException("The HTTPS response is empty.");
                    }
                    return output.ToArray();
                }
            }
        }

        private async Task DownloadArchive(
            ReleaseArchive archive,
            string destination,
            IProgress<UpdateProgress> progress)
        {
            progress?.Report(new UpdateProgress(UpdateStage.Downloading, 0, archive.Length));
            using (var cancellation = new CancellationTokenSource(
                TimeSpan.FromMilliseconds(_policy.ArchiveTimeoutMilliseconds)))
            using (var request = new HttpRequestMessage(HttpMethod.Get, archive.Uri))
            using (var response = await _client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellation.Token).ConfigureAwait(false))
            {
                RequireSuccess(response);
                if (response.Content.Headers.ContentLength.HasValue &&
                    response.Content.Headers.ContentLength.Value != archive.Length)
                {
                    throw new InvalidDataException("The archive HTTP length does not match the signed manifest.");
                }

                using (var input = await response.Content.ReadAsStreamAsync(cancellation.Token).ConfigureAwait(false))
                using (var output = new FileStream(
                    destination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.SequentialScan))
                using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                {
                    var buffer = new byte[64 * 1024];
                    long written = 0;
                    var lastReportedPercentage = 0;
                    while (true)
                    {
                        var read = await input.ReadAsync(buffer, cancellation.Token).ConfigureAwait(false);
                        if (read == 0) break;
                        if (written > archive.Length - read)
                        {
                            throw new InvalidDataException("The downloaded archive is longer than declared.");
                        }
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellation.Token).ConfigureAwait(false);
                        hash.AppendData(buffer, 0, read);
                        written += read;
                        var percentage = archive.Length <= 0
                            ? 0
                            : (int)(written * 100L / archive.Length);
                        if (percentage > lastReportedPercentage)
                        {
                            lastReportedPercentage = percentage;
                            progress?.Report(new UpdateProgress(UpdateStage.Downloading, written, archive.Length));
                        }
                    }
                    output.Flush(true);
                    var actualHash = ToLowerHex(hash.GetHashAndReset());
                    if (written != archive.Length ||
                        !string.Equals(actualHash, archive.Sha256, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("The downloaded archive does not match its signed identity.");
                    }
                }
            }
        }

        private void ExtractClosedArchive(
            string archivePath,
            string payloadDirectory,
            string partialRoot,
            IReadOnlyList<ReleaseFile> declaredFiles)
        {
            var expected = declaredFiles.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
            var partialIndexes = declaredFiles
                .Select((file, index) => new { file.Path, Index = index })
                .ToDictionary(item => item.Path, item => item.Index, StringComparer.OrdinalIgnoreCase);
            var extracted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var archivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var root = Path.GetFullPath(payloadDirectory).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;

            using (var archive = ZipFile.OpenRead(archivePath))
            {
                if (archive.Entries.Count > _policy.MaximumArchiveEntries)
                {
                    throw new InvalidDataException("The archive contains too many entries.");
                }
                foreach (var entry in archive.Entries)
                {
                    RejectLinkEntry(entry);
                    var isDirectory = entry.FullName.EndsWith("/", StringComparison.Ordinal);
                    var canonical = ContractCodec.RequireCanonicalRelativePath(entry.FullName, isDirectory);
                    if (!archivePaths.Add(canonical.TrimEnd('/')))
                    {
                        throw new InvalidDataException(
                            "The archive contains a duplicate file-system path: " + canonical);
                    }
                    if (isDirectory)
                    {
                        var prefix = canonical;
                        if (!expected.Keys.Any(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                        {
                            throw new InvalidDataException("The archive contains an undeclared directory: " + canonical);
                        }
                        continue;
                    }

                    ReleaseFile declared;
                    if (!expected.TryGetValue(canonical, out declared) ||
                        !string.Equals(declared.Path, canonical, StringComparison.Ordinal) ||
                        !extracted.Add(canonical))
                    {
                        throw new InvalidDataException("The archive contains an undeclared or duplicate file: " + canonical);
                    }
                    if (entry.Length != declared.Length)
                    {
                        throw new InvalidDataException("An archive entry length differs from the signed manifest: " + canonical);
                    }
                    var destination = Path.GetFullPath(Path.Combine(
                        payloadDirectory,
                        canonical.Replace('/', Path.DirectorySeparatorChar)));
                    if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("An archive entry escapes the payload root.");
                    }
                    var parent = Path.GetDirectoryName(destination);
                    if (string.IsNullOrEmpty(parent))
                    {
                        throw new InvalidDataException("An archive entry has no payload parent.");
                    }
                    Directory.CreateDirectory(parent);
                    var partialPath = Path.Combine(
                        partialRoot,
                        partialIndexes[canonical].ToString("D8") + ".tmp");
                    var ordinaryPartialRoot = Path.GetFullPath(partialRoot).TrimEnd(Path.DirectorySeparatorChar) +
                        Path.DirectorySeparatorChar;
                    if (!Path.GetFullPath(partialPath).StartsWith(
                            ordinaryPartialRoot,
                            StringComparison.OrdinalIgnoreCase) ||
                        (File.GetAttributes(partialRoot) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidDataException("The extraction partial path is unsafe.");
                    }

                    using (var input = entry.Open())
                    using (var output = new FileStream(
                        partialPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        64 * 1024,
                        FileOptions.SequentialScan))
                    using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                    {
                        var buffer = new byte[64 * 1024];
                        long written = 0;
                        while (true)
                        {
                            var read = input.Read(buffer, 0, buffer.Length);
                            if (read == 0) break;
                            if (written > declared.Length - read)
                            {
                                throw new InvalidDataException("An archive entry expands beyond its signed length: " + canonical);
                            }
                            output.Write(buffer, 0, read);
                            hash.AppendData(buffer, 0, read);
                            written += read;
                        }
                        output.Flush(true);
                        if (written != declared.Length ||
                            !string.Equals(ToLowerHex(hash.GetHashAndReset()), declared.Sha256, StringComparison.Ordinal))
                        {
                            throw new InvalidDataException("An extracted file differs from its signed identity: " + canonical);
                        }
                    }
                    File.Move(partialPath, destination, false);
                }
            }

            if (extracted.Count != expected.Count || expected.Keys.Any(path => !extracted.Contains(path)))
            {
                throw new InvalidDataException("The archive does not contain the complete signed file set.");
            }
        }

        private static void RejectLinkEntry(ZipArchiveEntry entry)
        {
            var unixFileType = (entry.ExternalAttributes >> 16) & 0xF000;
            var windowsAttributes = (FileAttributes)(entry.ExternalAttributes & 0xFFFF);
            if (unixFileType == 0xA000 || (windowsAttributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("The archive contains a link or reparse-point entry.");
            }
        }

        private static void RequireSuccess(HttpResponseMessage response)
        {
            if ((int)response.StatusCode < 200 || (int)response.StatusCode > 299)
            {
                throw new HttpRequestException("The static release endpoint returned HTTP " +
                    ((int)response.StatusCode).ToString() + ".");
            }
        }

        private static void WriteDurableFile(string path, byte[] bytes)
        {
            var temporaryPath = path + ".tmp";
            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temporaryPath, path, false);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static string ToLowerHex(byte[] bytes) =>
            BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}
