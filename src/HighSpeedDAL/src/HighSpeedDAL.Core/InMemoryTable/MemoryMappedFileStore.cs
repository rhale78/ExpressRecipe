using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.Logging;
using HighSpeedDAL.Core.Attributes;

namespace HighSpeedDAL.Core.InMemoryTable;

/// <summary>
/// Manages memory-mapped file storage for InMemoryTable with cross-process synchronization and schema validation.
/// File Format:
/// - Header (16KB fixed size)
///   - Magic: "HSDAL_MMF" (9 bytes)
///   - Version: byte (1 byte)
///   - Schema Hash: SHA256 (32 bytes)
///   - Row Count: int (4 bytes)
///   - Data Offset: long (8 bytes)
///   - Reserved: (remaining bytes for future use)
/// - Data Section (variable size, MessagePack serialized rows)
/// </summary>
public sealed class MemoryMappedFileStore<T> : IDisposable where T : class
{
    private const string MagicString = "HSDAL_MMF";
    private const byte Version = 1;
    private const int HeaderSize = 16 * 1024; // 16KB header
    private const long MinDataOffset = HeaderSize;

    private readonly string _fileName;
    private readonly long _fileSizeBytes;
    private readonly string _schemaHash;
    private readonly MemoryMappedSynchronizer _synchronizer;
    private readonly ILogger<MemoryMappedFileStore<T>> _logger;
    private readonly InMemoryTableAttribute _config;
    private readonly string _filePath;
    
    private MemoryMappedFile? _mmf;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the MemoryMappedFileStore.
    /// </summary>
    /// <param name="fileName">Name of the memory-mapped file (without extension)</param>
    /// <param name="config">Configuration from InMemoryTableAttribute</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="synchronizerLogger">Logger for synchronizer</param>
    public MemoryMappedFileStore(
        string fileName, 
        InMemoryTableAttribute config,
        ILogger<MemoryMappedFileStore<T>> logger,
        ILogger<MemoryMappedSynchronizer> synchronizerLogger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(synchronizerLogger);

        _fileName = fileName;
        _config = config;
        _logger = logger;
        _fileSizeBytes = config.MemoryMappedFileSizeMB * 1024L * 1024L;
        _schemaHash = ComputeSchemaHash();
        _synchronizer = new MemoryMappedSynchronizer(fileName, synchronizerLogger);

        // Determine file path
        string tempDir = Path.Combine(Path.GetTempPath(), "HighSpeedDAL");
        Directory.CreateDirectory(tempDir);
        _filePath = Path.Combine(tempDir, $"{fileName}.mmf");

        _logger.LogInformation("Initializing memory-mapped file store for '{FileName}' at '{FilePath}' with size {SizeMB}MB",
            fileName, _filePath, config.MemoryMappedFileSizeMB);

        InitializeFile();
    }

    /// <summary>
    /// Computes SHA256 hash of the entity schema for validation.
    /// Includes: type name, property names, types, and order.
    /// </summary>
    private static string ComputeSchemaHash()
    {
        var type = typeof(T);
        var sb = new StringBuilder();
        
        sb.Append(type.FullName);
        
        var properties = type.GetProperties()
            .OrderBy(p => p.Name)
            .Select(p => $"{p.Name}:{p.PropertyType.FullName}");
        
        foreach (var prop in properties)
        {
            sb.Append(';');
            sb.Append(prop);
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Initializes or validates the memory-mapped file.
    /// </summary>
    private void InitializeFile()
    {
        bool fileExists = File.Exists(_filePath);

        if (fileExists)
        {
            _logger.LogDebug("Memory-mapped file '{FilePath}' exists, validating schema...", _filePath);

            // For development/testing: if schema validation is likely to fail (due to code changes),
            // just delete and recreate instead of trying to validate with potentially incompatible schema
            if (_config.AutoCreateFile)
            {
                try
                {
                    _logger.LogDebug("Auto-create enabled, deleting existing file for clean initialization");
                    File.Delete(_filePath);
                    fileExists = false;
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Could not delete existing file '{FilePath}', attempting validation instead", _filePath);
                }
            }

                if (fileExists)
                {
                    // When AutoCreateFile=false, skip validation and use existing file directly
                    // This is common in reload scenarios where the schema hasn't changed
                    if (!_config.AutoCreateFile)
                    {
                        try
                        {
                            using var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                            _mmf = MemoryMappedFile.CreateFromFile(fileStream, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);
                            _logger.LogInformation("Memory-mapped file '{FileName}' opened successfully (validation skipped, AutoCreateFile=false)", _fileName);
                            return;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to open existing memory-mapped file '{FileName}'", _fileName);
                            throw;
                        }
                    }

                    // AutoCreateFile=true: Validate schema and recreate if needed
                    try
                    {
                        // Open existing file and validate schema
                        using var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        _mmf = MemoryMappedFile.CreateFromFile(fileStream, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);

                        ValidateSchema();
                        _logger.LogInformation("Memory-mapped file '{FileName}' validated successfully", _fileName);
                    }
                    catch (Exception ex)
                    {

                        _logger.LogWarning(ex, "Schema validation failed for '{FileName}', recreating file...", _fileName);

                        // Dispose MMF and wait for handles to release
                        _mmf?.Dispose();
                        _mmf = null;

                        // Give Windows time to release file handles
                        Thread.Sleep(100);

                        try
                        {
                            File.Delete(_filePath);
                            fileExists = false;
                        }
                        catch (IOException deleteEx)
                        {
                            _logger.LogError(deleteEx, "Failed to delete invalid schema file '{FilePath}'. Manual cleanup may be required.", _filePath);
                            throw new InvalidOperationException($"Cannot recreate memory-mapped file '{_fileName}' - file is locked. Please delete {_filePath} manually and restart.", deleteEx);
                        }
                    }
                }
            }

        if (!fileExists)
        {
            if (!_config.AutoCreateFile)
            {
                throw new FileNotFoundException($"Memory-mapped file '{_filePath}' does not exist and AutoCreateFile is disabled");
            }

            _logger.LogInformation("Creating new memory-mapped file '{FileName}'...", _fileName);
            CreateNewFile();
        }
    }

    /// <summary>
    /// Creates a new memory-mapped file with header.
    /// </summary>
    private void CreateNewFile()
    {
        // Create physical file first
        using (var fileStream = new FileStream(_filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            fileStream.SetLength(_fileSizeBytes);
        }

        // Open as memory-mapped file
        using var fileStream2 = new FileStream(_filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        _mmf = MemoryMappedFile.CreateFromFile(fileStream2, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);

        // Write header
        WriteHeader(0, MinDataOffset);
        _logger.LogInformation("Created memory-mapped file '{FileName}' with size {SizeMB}MB", _fileName, _config.MemoryMappedFileSizeMB);
    }

    /// <summary>
    /// Validates the file header schema matches current entity schema.
    /// </summary>
    private void ValidateSchema()
    {
        ArgumentNullException.ThrowIfNull(_mmf);

        using var accessor = _mmf.CreateViewAccessor(0, HeaderSize, MemoryMappedFileAccess.Read);
        
        // Read magic string
        byte[] magicBytes = new byte[MagicString.Length];
        accessor.ReadArray(0, magicBytes, 0, magicBytes.Length);
        string magic = Encoding.ASCII.GetString(magicBytes);
        
        if (magic != MagicString)
        {
            throw new InvalidDataException($"Invalid magic string: expected '{MagicString}', got '{magic}'");
        }

        // Read version
        byte version = accessor.ReadByte(MagicString.Length);
        if (version != Version)
        {
            throw new InvalidDataException($"Unsupported version: expected {Version}, got {version}");
        }

        // Read schema hash
        byte[] hashBytes = new byte[32];
        accessor.ReadArray(MagicString.Length + 1, hashBytes, 0, 32);
        string fileSchemaHash = Convert.ToHexString(hashBytes);

        if (fileSchemaHash != _schemaHash)
        {
            throw new InvalidDataException($"Schema mismatch: file hash {fileSchemaHash}, expected {_schemaHash}");
        }

        _logger.LogDebug("Schema validated for '{FileName}': {SchemaHash}", _fileName, _schemaHash);
    }

    /// <summary>
    /// Writes the file header.
    /// </summary>
    private void WriteHeader(int rowCount, long dataOffset)
    {
        ArgumentNullException.ThrowIfNull(_mmf);

        using var accessor = _mmf.CreateViewAccessor(0, HeaderSize, MemoryMappedFileAccess.Write);
        
        int offset = 0;

        // Write magic string
        byte[] magicBytes = Encoding.ASCII.GetBytes(MagicString);
        accessor.WriteArray(offset, magicBytes, 0, magicBytes.Length);
        offset += magicBytes.Length;

        // Write version
        accessor.Write(offset, Version);
        offset += 1;

        // Write schema hash
        byte[] hashBytes = Convert.FromHexString(_schemaHash);
        accessor.WriteArray(offset, hashBytes, 0, hashBytes.Length);
        offset += hashBytes.Length;

        // Write row count
        accessor.Write(offset, rowCount);
        offset += sizeof(int);

        // Write data offset
        accessor.Write(offset, dataOffset);
        
        _logger.LogTrace("Wrote header for '{FileName}': rowCount={RowCount}, dataOffset={DataOffset}", 
            _fileName, rowCount, dataOffset);
    }

    /// <summary>
    /// Loads all rows from the memory-mapped file.
    /// </summary>
    public async Task<List<T>> LoadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(_mmf);

        using var readLock = await _synchronizer.AcquireReadLockAsync(cancellationToken);
        
        using var accessor = _mmf.CreateViewAccessor(0, HeaderSize, MemoryMappedFileAccess.Read);
        
        // Read row count and data offset from header
        int rowCount = accessor.ReadInt32(MagicString.Length + 1 + 32);
        long dataOffset = accessor.ReadInt64(MagicString.Length + 1 + 32 + sizeof(int));

        if (rowCount == 0)
        {
            _logger.LogDebug("No rows to load from '{FileName}'", _fileName);
            return [];
        }

        // Calculate data length
        long dataLength = _fileSizeBytes - dataOffset;
        if (dataLength <= 0)
        {
            _logger.LogWarning("Invalid data section in '{FileName}': offset={DataOffset}, fileSize={FileSize}",
                _fileName, dataOffset, _fileSizeBytes);
            return [];
        }

        // Read data section
        using var dataAccessor = _mmf.CreateViewAccessor(dataOffset, dataLength, MemoryMappedFileAccess.Read);
        byte[] data = new byte[dataLength];
        dataAccessor.ReadArray(0, data, 0, (int)dataLength);

        // Find actual data end (MessagePack may not use full space)
        int actualDataLength = FindDataEnd(data);
        if (actualDataLength == 0)
        {
            _logger.LogDebug("Empty data section in '{FileName}'", _fileName);
            return [];
        }

        // Deserialize
        var rows = MessagePackSerializer.Deserialize<List<T>>(data.AsMemory(0, actualDataLength));
        
        _logger.LogInformation("Loaded {RowCount} rows from '{FileName}'", rows.Count, _fileName);
        return rows;
    }

    /// <summary>
    /// Finds the end of actual MessagePack data (skips trailing zeros).
    /// </summary>
    private static int FindDataEnd(byte[] data)
    {
        for (int i = data.Length - 1; i >= 0; i--)
        {
            if (data[i] != 0)
                return i + 1;
        }
        return 0;
    }

    /// <summary>
    /// Saves all rows to the memory-mapped file.
    /// </summary>
    public async Task SaveAsync(List<T> rows, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(_mmf);
        ArgumentNullException.ThrowIfNull(rows);

        using var writeLock = await _synchronizer.AcquireWriteLockAsync(cancellationToken);

        // Serialize data
        byte[] data = MessagePackSerializer.Serialize(rows);
        
        if (data.Length + MinDataOffset > _fileSizeBytes)
        {
            throw new InvalidOperationException(
                $"Data size ({data.Length} bytes) exceeds file capacity ({_fileSizeBytes - MinDataOffset} bytes). " +
                $"Increase MemoryMappedFileSizeMB (current: {_config.MemoryMappedFileSizeMB}MB)");
        }

        // Write data section
        using var dataAccessor = _mmf.CreateViewAccessor(MinDataOffset, data.Length, MemoryMappedFileAccess.Write);
        dataAccessor.WriteArray(0, data, 0, data.Length);

        // Update header
        WriteHeader(rows.Count, MinDataOffset);

        _logger.LogInformation("Saved {RowCount} rows to '{FileName}' ({DataSizeKB}KB)", 
            rows.Count, _fileName, data.Length / 1024);
    }

        /// <summary>
        /// Deletes the memory-mapped file from disk.
        /// Warning: This will permanently delete the file. Ensure no other processes are accessing it.
        /// </summary>
        public void DeleteFile()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                    _logger.LogInformation("Deleted memory-mapped file '{FilePath}'", _filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete memory-mapped file '{FilePath}'", _filePath);
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _mmf?.Dispose();
                _synchronizer?.Dispose();

                // Delete file if configured
                if (_config.DeleteFileOnDispose)
                {
                    try
                    {
                        if (File.Exists(_filePath))
                        {
                            File.Delete(_filePath);
                            _logger.LogInformation("Deleted memory-mapped file on dispose: '{FilePath}'", _filePath);
                        }
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "Failed to delete memory-mapped file on dispose: '{FilePath}'", _filePath);
                        // Don't throw - dispose should be best-effort
                    }
                }

                _logger.LogDebug("Disposed memory-mapped file store for '{FileName}'", _fileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing memory-mapped file store for '{FileName}'", _fileName);
            }

            _disposed = true;
        }
    }
