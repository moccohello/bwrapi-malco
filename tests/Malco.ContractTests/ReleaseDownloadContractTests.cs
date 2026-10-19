using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Malco.Launcher;
using Xunit;

namespace Malco.ContractTests
{
    public sealed class ReleaseDownloadContractTests
    {
        private const string Notes = "{\"schema\":\"malco.release-notes.v1\",\"ko\":\"한국어\",\"en\":\"English\"}";

        [Fact]
        public void NotesAcceptExactLimitAndUseTheArchiveSibling()
        {
            var bytes = new byte[ContractCodec.MaximumReleaseNotesBytes];
            Array.Fill(bytes, (byte)' ');
            Encoding.UTF8.GetBytes(Notes).CopyTo(bytes, 0);
            using var installer = CreateInstaller((request, _) =>
            {
                Assert.Equal("https://update.horangmalco.com/releases/1.0.0/notes.json", request.RequestUri.AbsoluteUri);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bytes)
                });
            });
            Assert.Equal("한국어", installer.TryFetchReleaseNote(Envelope(), true));
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.Redirect)]
        public void NotesHttpFailuresRemainOptional(HttpStatusCode status)
        {
            using var installer = CreateInstaller((_, _) => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(Notes),
                Headers = { Location = new Uri("https://other.invalid/notes.json") }
            }));
            Assert.Null(installer.TryFetchReleaseNote(Envelope(), false));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(16385)]
        public void NotesRejectEmptyOrOversizedDeclaredLength(long length)
        {
            using var installer = CreateInstaller((_, _) =>
            {
                var content = new ByteArrayContent(length == 0 ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(Notes));
                content.Headers.ContentLength = length;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            });
            Assert.Null(installer.TryFetchReleaseNote(Envelope(), false));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(16385)]
        public void NotesRejectEmptyOrOversizedBodyWithoutDeclaredLength(int length)
        {
            var bytes = new byte[length];
            if (length != 0)
            {
                Array.Fill(bytes, (byte)' ');
                Encoding.UTF8.GetBytes(Notes).CopyTo(bytes, 0);
            }
            using var installer = CreateInstaller((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new UnknownLengthContent(bytes)
            }));
            Assert.Null(installer.TryFetchReleaseNote(Envelope(), false));
        }

        [Fact]
        public void NotesTimeoutRemainsOptional()
        {
            using var installer = CreateInstaller(async (_, cancellation) =>
            {
                await Task.Delay(Timeout.Infinite, cancellation);
                throw new InvalidOperationException("The timeout must cancel the request.");
            });
            Assert.Null(installer.TryFetchReleaseNote(Envelope(), false));
        }

        [Theory]
        [InlineData("\r\n", false)]
        [InlineData("first\r\nsecond\r\n", true)]
        public void NotesNormalizationPreservesWhitespaceRejection(string text, bool accepted)
        {
            string json = System.Text.Json.JsonSerializer.Serialize(new
            {
                schema = "malco.release-notes.v1",
                ko = text,
                en = text
            });
            string result = ContractCodec.TryReadReleaseNote(Encoding.UTF8.GetBytes(json), false);
            if (accepted) Assert.Equal("first\nsecond", result);
            else Assert.Null(result);
        }

        private static VerifiedEnvelope Envelope() => new VerifiedEnvelope
        {
            Manifest = new ReleaseManifest
            {
                Archive = new ReleaseArchive
                {
                    Uri = new Uri("https://update.horangmalco.com/releases/1.0.0/payload.zip")
                }
            }
        };

        private static ReleaseInstaller CreateInstaller(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var policy = new LauncherPolicy
            {
                PublicKeySubjectPublicKeyInfo = key.ExportSubjectPublicKeyInfo(),
                FeedTimeoutMilliseconds = 100
            };
            var installer = new ReleaseInstaller(policy, new ReleaseVerifier(policy), new InstallStateStore(Path.GetTempPath()));
            // Keep HTTP injection confined to tests; product construction still disables redirects.
            var field = typeof(ReleaseInstaller).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            ((HttpClient)field.GetValue(installer)).Dispose();
            field.SetValue(installer, new HttpClient(new FakeHandler(send)));
            return installer;
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;
            public FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => _send = send;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                _send(request, cancellationToken);
        }

        private sealed class UnknownLengthContent : HttpContent
        {
            private readonly byte[] _bytes;
            public UnknownLengthContent(byte[] bytes) => _bytes = bytes;
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) =>
                stream.WriteAsync(_bytes, 0, _bytes.Length);
            protected override bool TryComputeLength(out long length)
            {
                length = 0;
                return false;
            }
        }
    }
}
