using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BwrApi.Client;
using Malco.Data;
using Xunit;

namespace Malco.ContractTests
{
    public sealed class ObserverConnectionContractTests
    {
        [Fact]
        public void TemporaryStartupFailuresRecoverWithoutRestart()
        {
            var attempts = 0;
            var waiting = 0;
            var result = BwrApiEmbeddedRuntimeProvider.OpenObserverWithRetry(
                () =>
                {
                    if (++attempts < 3) throw new BwrApiHostUnavailableException("startup");
                    return "connected";
                },
                _ => waiting++,
                CancellationToken.None);
            Assert.Equal("connected", result);
            Assert.Equal(3, attempts);
            Assert.Equal(2, waiting);
        }

        [Fact]
        public void CancellationInterruptsStartupWait()
        {
            using var stopping = new CancellationTokenSource();
            var attempts = 0;
            var elapsed = Stopwatch.StartNew();
            Assert.Throws<OperationCanceledException>(() =>
                BwrApiEmbeddedRuntimeProvider.OpenObserverWithRetry<string>(
                    () => { attempts++; throw new BwrApiHostUnavailableException("startup"); },
                    _ => stopping.Cancel(),
                    stopping.Token));
            Assert.Equal(1, attempts);
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(2));
        }

        [Fact]
        public void AlreadyCancelledDoesNotAttemptToOpen()
        {
            using var stopping = new CancellationTokenSource();
            stopping.Cancel();
            var attempts = 0;
            Assert.Throws<OperationCanceledException>(() =>
                BwrApiEmbeddedRuntimeProvider.OpenObserverWithRetry(
                    () => { attempts++; return "connected"; },
                    _ => { },
                    stopping.Token));
            Assert.Equal(0, attempts);
        }

        [Fact]
        public void ArtifactFailureIsNotRetried()
        {
            var attempts = 0;
            var waiting = 0;
            Assert.Throws<BwrApiNativeArtifactException>(() =>
                BwrApiEmbeddedRuntimeProvider.OpenObserverWithRetry<string>(
                    () => { attempts++; throw new BwrApiNativeArtifactException("hash", "invalid artifact"); },
                    _ => waiting++,
                    CancellationToken.None));
            Assert.Equal(1, attempts);
            Assert.Equal(0, waiting);
        }

        [Fact]
        public void ChangedProcessIdentityIsNotRetried()
        {
            var attempts = 0;
            Assert.Throws<InvalidOperationException>(() =>
                BwrApiEmbeddedRuntimeProvider.OpenObserverWithRetry<string>(
                    () => { attempts++; throw new InvalidOperationException("tracked identity changed"); },
                    _ => { },
                    CancellationToken.None));
            Assert.Equal(1, attempts);
        }

        [Fact]
        public void PersistentAccessFailureExpiresAndPreservesLastError()
        {
            var attempts = 0;
            var cause = new BwrApiHostUnavailableException("open_target_process");
            var error = Assert.Throws<TimeoutException>(() =>
                BwrApiEmbeddedRuntimeProvider.OpenObserverWithRetry<string>(
                    () => { attempts++; throw cause; },
                    _ => { },
                    CancellationToken.None,
                    TimeSpan.FromMilliseconds(30)));
            Assert.True(attempts >= 1);
            Assert.Same(cause, error.InnerException);
            Assert.Contains("open_target_process", error.Message);
        }

        [Fact]
        public async Task DelayedCancellationInterruptsRetryWait()
        {
            using var stopping = new CancellationTokenSource();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var worker = Task.Run(() => BwrApiEmbeddedRuntimeProvider.OpenObserverWithRetry<string>(
                () => throw new BwrApiHostUnavailableException("startup"),
                _ => entered.TrySetResult(),
                stopping.Token));
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
                stopping.CancelAfter(TimeSpan.FromMilliseconds(50));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    async () => await worker.WaitAsync(TimeSpan.FromSeconds(2)));
            }
            finally
            {
                stopping.Cancel();
            }
        }

        [Fact]
        public void NativeCreateFailureIsNotRetriedAsProcessUnavailability()
        {
            var attempts = 0;
            var waiting = 0;
            Assert.Throws<BwrApiNativeCallException>(() =>
                BwrApiEmbeddedRuntimeProvider.OpenObserverWithRetry<string>(
                    () => { attempts++; throw new BwrApiNativeCallException("create", BwrApiResultCode.InvalidArgument); },
                    _ => waiting++,
                    CancellationToken.None));
            Assert.Equal(1, attempts);
            Assert.Equal(0, waiting);
        }
    }
}
