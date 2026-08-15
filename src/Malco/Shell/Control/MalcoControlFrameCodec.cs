using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Malco.Shell.Control
{
    internal static class MalcoControlFrameCodec
    {
        private const byte ProtocolVersion = 1;
        private const byte QuitCommand = 1;
        internal const byte AcceptedStatus = 0;
        internal const byte RefusedStatus = 1;
        internal const byte ProtocolErrorStatus = 2;
        private const int NonceLength = 32;
        private const int ChallengeFrameLength = 4 + 1 + NonceLength;
        private const int RequestFrameLength = 4 + 1 + 1 + NonceLength + NonceLength;
        private const int ResponseFrameLength = 4 + 1 + 1 + NonceLength;
        private static readonly byte[] Magic = { (byte)'M', (byte)'L', (byte)'C', (byte)'1' };

        internal static byte Accepted => AcceptedStatus;
        internal static byte Refused => RefusedStatus;
        internal static byte InvalidProtocol => ProtocolErrorStatus;
        internal static int ChallengeBytes => ChallengeFrameLength;
        internal static int RequestBytes => RequestFrameLength;
        internal static int ResponseBytes => ResponseFrameLength;
        internal static int NonceBytes => NonceLength;

        internal static byte[] CreateChallengeFrame(byte[] challenge)
        {
            var frame = new byte[ChallengeFrameLength];
            CopyHeader(frame);
            frame[4] = ProtocolVersion;
            Buffer.BlockCopy(challenge, 0, frame, 5, NonceLength);
            return frame;
        }

        internal static bool TryReadChallenge(
            byte[] frame,
            out byte[] challenge)
        {
            challenge = new byte[NonceLength];
            if (frame.Length != ChallengeFrameLength ||
                !HasHeader(frame) ||
                frame[4] != ProtocolVersion)
            {
                return false;
            }
            Buffer.BlockCopy(frame, 5, challenge, 0, NonceLength);
            return true;
        }

        internal static byte[] CreateQuitRequestFrame(
            byte[] challenge,
            byte[] requestNonce)
        {
            var request = new byte[RequestFrameLength];
            CopyHeader(request);
            request[4] = ProtocolVersion;
            request[5] = QuitCommand;
            Buffer.BlockCopy(challenge, 0, request, 6, NonceLength);
            Buffer.BlockCopy(requestNonce, 0, request, 6 + NonceLength, NonceLength);
            return request;
        }

        internal static bool TryValidateQuitRequest(
            byte[] request,
            byte[] challenge,
            out byte[] requestNonce)
        {
            requestNonce = new byte[NonceLength];
            if (request.Length != RequestFrameLength)
            {
                return false;
            }
            Buffer.BlockCopy(request, 6 + NonceLength, requestNonce, 0, NonceLength);
            return HasHeader(request) &&
                   request[4] == ProtocolVersion &&
                   request[5] == QuitCommand &&
                   CryptographicOperations.FixedTimeEquals(
                       request.AsSpan(6, NonceLength),
                       challenge);
        }

        internal static byte[] CreateResponseFrame(byte status, byte[] requestNonce)
        {
            var frame = new byte[ResponseFrameLength];
            CopyHeader(frame);
            frame[4] = ProtocolVersion;
            frame[5] = status;
            Buffer.BlockCopy(requestNonce, 0, frame, 6, NonceLength);
            return frame;
        }

        internal static bool TryReadResponseStatus(
            byte[] response,
            byte[] requestNonce,
            out byte status)
        {
            status = 0;
            if (response.Length != ResponseFrameLength ||
                !HasHeader(response) ||
                response[4] != ProtocolVersion ||
                !CryptographicOperations.FixedTimeEquals(
                    response.AsSpan(6, NonceLength),
                    requestNonce))
            {
                return false;
            }
            status = response[5];
            return true;
        }

        internal static async Task ReadExactlyAsync(
            Stream stream,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset != buffer.Length)
            {
                var count = await stream.ReadAsync(
                    buffer,
                    offset,
                    buffer.Length - offset,
                    cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    throw new EndOfStreamException("Malco control frame ended early.");
                }
                offset += count;
            }
        }

        internal static async Task WriteFrameAsync(
            Stream stream,
            byte[] frame,
            CancellationToken cancellationToken)
        {
            await stream.WriteAsync(
                frame,
                0,
                frame.Length,
                cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private static bool HasHeader(byte[] frame)
        {
            return frame.Length >= Magic.Length &&
                   CryptographicOperations.FixedTimeEquals(
                       frame.AsSpan(0, Magic.Length),
                       Magic);
        }

        private static void CopyHeader(byte[] frame)
        {
            Buffer.BlockCopy(Magic, 0, frame, 0, Magic.Length);
        }
    }
}
