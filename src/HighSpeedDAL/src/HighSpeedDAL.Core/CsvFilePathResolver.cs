using System;
using System.IO;

namespace HighSpeedDAL.Core.Utilities;

/// <summary>
/// Resolves CSV file paths from ReferenceTable attributes.
/// Handles both relative and absolute paths intelligently.
/// 
/// HighSpeedDAL Framework v0.1
/// </summary>
public static class CsvFilePathResolver
{
    /// <summary>
    /// Resolves a CSV file path to an absolute path.
    /// 
    /// Logic:
    /// 1. Try as relative path from application base directory
    /// 2. If not found or path appears absolute, use as-is
    /// 
    /// Examples:
    /// - "Data/States.csv" → "{AppRoot}/Data/States.csv"
    /// - "C:\Data\States.csv" → "C:\Data\States.csv"
    /// - "/data/states.csv" → "/data/states.csv"
    /// </summary>
    public static string Resolve(string csvFilePath)
    {
        if (string.IsNullOrWhiteSpace(csvFilePath))
        {
            return string.Empty;
        }

        // Check if path appears to be absolute
        if (IsAbsolutePath(csvFilePath))
        {
            return csvFilePath;
        }

        // Try as relative path from application base directory
        string appBasePath = AppContext.BaseDirectory;
        string relativePath = Path.Combine(appBasePath, csvFilePath);

        // If file exists at relative path, use it
        if (File.Exists(relativePath))
        {
            return Path.GetFullPath(relativePath);
        }

        // File not found at relative path, try as absolute
        // (maybe user provided absolute path without drive letter on Windows)
        if (File.Exists(csvFilePath))
        {
            return Path.GetFullPath(csvFilePath);
        }

        // Return the relative path attempt - caller will handle file not found
        return Path.GetFullPath(relativePath);
    }

    /// <summary>
    /// Checks if a path appears to be absolute (vs relative)
    /// </summary>
    private static bool IsAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Windows absolute paths: C:\, D:\, \\server\share
        if (path.Length >= 2)
        {
            // Drive letter: C:\
            if (char.IsLetter(path[0]) && path[1] == ':')
            {
                return true;
            }

            // UNC path: \\server\share
            if (path[0] == '\\' && path[1] == '\\')
            {
                return true;
            }
        }

        // Unix absolute path: /data/file.csv
        if (path[0] == '/')
        {
            return true;
        }

        return false;
    }
}
