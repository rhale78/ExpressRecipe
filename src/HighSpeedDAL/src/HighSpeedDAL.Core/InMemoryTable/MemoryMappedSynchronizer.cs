using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.Core.InMemoryTable
{
    /// <summary>
    /// Provides cross-process synchronization for memory-mapped file operations.
    /// Uses named Semaphore for write locks (async-safe, no thread affinity) and read coordination.
    /// </summary>
    public sealed class MemoryMappedSynchronizer : IDisposable
    {
        private readonly string _fileName;
        private readonly Semaphore _writeSemaphore;  // Changed from Mutex to Semaphore (async-safe!)
        private readonly Semaphore _readSemaphore;
        private readonly ILogger<MemoryMappedSynchronizer> _logger;
        private readonly TimeSpan _lockTimeout;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the MemoryMappedSynchronizer.
        /// </summary>
        /// <param name="fileName">Name of the memory-mapped file (used to create unique sync objects)</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="lockTimeoutMs">Lock acquisition timeout in milliseconds (default: 5000ms)</param>
        public MemoryMappedSynchronizer(string fileName, ILogger<MemoryMappedSynchronizer> logger, int lockTimeoutMs = 5000)
        {
            _fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lockTimeout = TimeSpan.FromMilliseconds(lockTimeoutMs);

            // Create named synchronization objects with Global\ prefix for cross-session access
            string writeSemaphoreName = $@"Global\HighSpeedDAL_MMF_{fileName}_WriteSemaphore";
            string readSemaphoreName = $@"Global\HighSpeedDAL_MMF_{fileName}_ReadSemaphore";

            try
            {
                // Use Semaphore instead of Mutex - allows Release() from any thread (async-safe!)
                _writeSemaphore = new Semaphore(1, 1, writeSemaphoreName); // Binary semaphore (0 or 1)
                _readSemaphore = new Semaphore(100, 100, readSemaphoreName); // Allow 100 concurrent readers

                _logger.LogDebug("Initialized synchronizer for file '{FileName}' with write semaphore '{WriteSemaphoreName}' and read semaphore '{ReadSemaphoreName}'",
                    fileName, writeSemaphoreName, readSemaphoreName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize synchronizer for file '{FileName}'", fileName);
                throw;
            }
        }

        /// <summary>
        /// Acquires a write lock. Blocks concurrent reads and writes.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A disposable lock handle. Dispose to release the lock.</returns>
        /// <exception cref="TimeoutException">Thrown if lock cannot be acquired within timeout period</exception>
        public async Task<IDisposable> AcquireWriteLockAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            try
            {
                // Wait for write lock with timeout (uses Semaphore instead of Mutex)
                bool acquired = await Task.Run(() => _writeSemaphore.WaitOne(_lockTimeout), cancellationToken);

                if (!acquired)
                {
                    throw new TimeoutException($"Failed to acquire write lock for '{_fileName}' within {_lockTimeout.TotalMilliseconds}ms");
                }

                _logger.LogTrace("Acquired write lock for file '{FileName}'", _fileName);
                return new WriteLockHandle(_writeSemaphore, _fileName, _logger);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Write lock acquisition cancelled for file '{FileName}'", _fileName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to acquire write lock for file '{FileName}'", _fileName);
                throw;
            }
        }

        /// <summary>
        /// Acquires a read lock. Allows concurrent reads but blocks concurrent writes.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A disposable lock handle. Dispose to release the lock.</returns>
        /// <exception cref="TimeoutException">Thrown if lock cannot be acquired within timeout period</exception>
        public async Task<IDisposable> AcquireReadLockAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            try
            {
                // Wait for read semaphore slot with timeout
                bool acquired = await Task.Run(() => _readSemaphore.WaitOne(_lockTimeout), cancellationToken);

                if (!acquired)
                {
                    throw new TimeoutException($"Failed to acquire read lock for '{_fileName}' within {_lockTimeout.TotalMilliseconds}ms");
                }

                _logger.LogTrace("Acquired read lock for file '{FileName}'", _fileName);
                return new ReadLockHandle(_readSemaphore, _fileName, _logger);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Read lock acquisition cancelled for file '{FileName}'", _fileName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to acquire read lock for file '{FileName}'", _fileName);
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _writeSemaphore?.Dispose();
                _readSemaphore?.Dispose();
                _logger.LogDebug("Disposed synchronizer for file '{FileName}'", _fileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing synchronizer for file '{FileName}'", _fileName);
            }

            _disposed = true;
        }

        /// <summary>
        /// Write lock handle that releases the semaphore on disposal.
        /// Semaphore.Release() can be called from any thread (unlike Mutex.ReleaseMutex()).
        /// </summary>
        private sealed class WriteLockHandle(Semaphore semaphore, string fileName, ILogger logger) : IDisposable
        {
            private bool _released;

            public void Dispose()
            {
                if (_released)
                {
                    return;
                }

                try
                {
                    semaphore.Release();  // Async-safe! Works from any thread
                    logger.LogTrace("Released write lock for file '{FileName}'", fileName);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error releasing write lock for file '{FileName}'", fileName);
                }

                _released = true;
            }
        }

        /// <summary>
        /// Read lock handle that releases the semaphore on disposal.
        /// </summary>
        private sealed class ReadLockHandle(Semaphore semaphore, string fileName, ILogger logger) : IDisposable
        {
            private bool _released;

            public void Dispose()
            {
                if (_released)
                {
                    return;
                }

                try
                {
                    semaphore.Release();
                    logger.LogTrace("Released read lock for file '{FileName}'", fileName);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error releasing read lock for file '{FileName}'", fileName);
                }

                _released = true;
            }
        }
    }
}
