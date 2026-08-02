using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace Malco.Launcher
{
    internal sealed class ReleaseVerifier
    {
        private const uint GenericRead = 0x80000000;
        private const uint FileShareRead = 0x00000001;
        private const uint OpenExisting = 3;
        private const uint FileFlagOpenReparsePoint = 0x00200000;
        private const uint FileFlagSequentialScan = 0x08000000;
        private const uint FileAttributeDirectory = 0x00000010;
        private const uint FileAttributeReparsePoint = 0x00000400;
        private const string P256Oid = "1.2.840.10045.3.1.7";
        private readonly LauncherPolicy _policy;

        public ReleaseVerifier(LauncherPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            ValidatePublicKey();
        }

        public int MaximumEnvelopeBytes => _policy.MaximumEnvelopeBytes;

        public VerifiedEnvelope VerifyEnvelope(byte[] envelopeBytes)
        {
            var parts = ContractCodec.ParseEnvelope(envelopeBytes, _policy);
            using (var verifier = CreateVerifier())
            {
                if (!verifier.VerifyData(
                    parts.SignedBytes,
                    parts.SignatureBytes,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                {
                    throw new CryptographicException("The release envelope signature is invalid.");
                }
            }

            var manifest = ContractCodec.ParseReleaseManifest(parts.SignedBytes, _policy);
            return new VerifiedEnvelope
            {
                EnvelopeBytes = (byte[])envelopeBytes.Clone(),
                ManifestSha256 = Sha256Hex(parts.SignedBytes),
                Manifest = manifest
            };
        }

        public VerifiedEnvelope VerifyInstalledRelease(
            ReleaseReference reference,
            string versionDirectory)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            var envelopePath = Path.Combine(versionDirectory, "release-envelope.json");
            var payloadPath = Path.Combine(versionDirectory, "payload");
            var envelope = VerifyEnvelope(ReadBoundedFile(envelopePath, _policy.MaximumEnvelopeBytes));
            if (envelope.Manifest.Sequence != reference.Sequence ||
                !string.Equals(envelope.ManifestSha256, reference.ManifestSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The installed release does not match its selector reference.");
            }
            VerifyPayloadTree(payloadPath, envelope.Manifest.Files);
            return envelope;
        }

        public void VerifyPayloadTree(string payloadRoot, IReadOnlyList<ReleaseFile> declaredFiles)
        {
            if (!Directory.Exists(payloadRoot))
            {
                throw new DirectoryNotFoundException("The release payload directory is missing.");
            }
            var root = Path.GetFullPath(payloadRoot).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if ((File.GetAttributes(payloadRoot) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("The payload root is a reparse point.");
            }
            var expected = declaredFiles.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
            var observed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(Path.GetFullPath(payloadRoot));

            while (pendingDirectories.Count != 0)
            {
                var directory = pendingDirectories.Pop();
                foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                {
                    var fullPath = Path.GetFullPath(path);
                    RequireChildPath(root, fullPath);
                    var attributes = File.GetAttributes(fullPath);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidDataException("The payload contains a reparse point.");
                    }
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        pendingDirectories.Push(fullPath);
                        continue;
                    }

                    var info = new FileInfo(fullPath);
                    var relative = fullPath.Substring(root.Length).Replace('\\', '/');
                    ContractCodec.RequireCanonicalRelativePath(relative, false);
                    ReleaseFile expectedFile;
                    if (!expected.TryGetValue(relative, out expectedFile) ||
                        !string.Equals(expectedFile.Path, relative, StringComparison.Ordinal) ||
                        !observed.Add(relative))
                    {
                        throw new InvalidDataException("The payload contains an undeclared or duplicate file: " + relative);
                    }
                    if (info.Length != expectedFile.Length ||
                        !string.Equals(Sha256File(fullPath), expectedFile.Sha256, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("The payload file does not match its signed identity: " + relative);
                    }
                }
            }

            if (observed.Count != expected.Count || expected.Keys.Any(path => !observed.Contains(path)))
            {
                throw new InvalidDataException("The payload tree is incomplete.");
            }
        }

        public static string Sha256File(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha256 = SHA256.Create())
            {
                return ToLowerHex(sha256.ComputeHash(stream));
            }
        }

        public static string Sha256Hex(byte[] bytes)
        {
            using (var sha256 = SHA256.Create())
            {
                return ToLowerHex(sha256.ComputeHash(bytes));
            }
        }

        public static byte[] ReadBoundedFile(string path, int maximumBytes)
        {
            if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            var fullPath = Path.GetFullPath(path);
            using (var handle = CreateFile(
                fullPath,
                GenericRead,
                FileShareRead,
                IntPtr.Zero,
                OpenExisting,
                FileFlagOpenReparsePoint | FileFlagSequentialScan,
                IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    throw new IOException(
                        "A required file could not be opened as a locked snapshot: " + fullPath,
                        new Win32Exception(Marshal.GetLastWin32Error()));
                }
                NativeFileInformation information;
                if (!GetFileInformationByHandle(handle, out information))
                {
                    throw new IOException(
                        "A required file snapshot could not be inspected: " + fullPath,
                        new Win32Exception(Marshal.GetLastWin32Error()));
                }
                if ((information.FileAttributes &
                        (FileAttributeDirectory | FileAttributeReparsePoint)) != 0)
                {
                    throw new InvalidDataException("A required file is not an ordinary file: " + fullPath);
                }
                var length = ((long)information.FileSizeHigh << 32) | information.FileSizeLow;
                if (length <= 0 || length > maximumBytes)
                {
                    throw new InvalidDataException(
                        "A required file is missing or exceeds its size limit: " + fullPath);
                }
                var bytes = new byte[checked((int)length)];
                using (var stream = new FileStream(handle, FileAccess.Read))
                {
                    var offset = 0;
                    while (offset < bytes.Length)
                    {
                        var read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read == 0)
                        {
                            throw new EndOfStreamException("A required file snapshot ended early: " + fullPath);
                        }
                        offset += read;
                    }
                    if (stream.ReadByte() != -1)
                    {
                        throw new InvalidDataException("A required file snapshot changed length: " + fullPath);
                    }
                }
                return bytes;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle file,
            out NativeFileInformation fileInformation);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeFileInformation
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        private void ValidatePublicKey()
        {
            using (var verifier = CreateVerifier())
            {
                var parameters = verifier.ExportParameters(false);
                if (verifier.KeySize != 256 ||
                    !string.Equals(parameters.Curve.Oid.Value, P256Oid, StringComparison.Ordinal))
                {
                    throw new CryptographicException("The launcher policy key must be ECDSA P-256.");
                }
            }
        }

        private ECDsa CreateVerifier()
        {
            var verifier = ECDsa.Create();
            try
            {
                int consumed;
                verifier.ImportSubjectPublicKeyInfo(_policy.PublicKeySubjectPublicKeyInfo, out consumed);
                if (consumed != _policy.PublicKeySubjectPublicKeyInfo.Length)
                {
                    throw new CryptographicException("The launcher policy public key has trailing data.");
                }
                return verifier;
            }
            catch
            {
                verifier.Dispose();
                throw;
            }
        }

        private static string ToLowerHex(byte[] bytes) =>
            BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();

        private static void RequireChildPath(string rootWithSeparator, string child)
        {
            var fullPath = Path.GetFullPath(child);
            if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("A payload path escapes its release root.");
            }
        }
    }
}
