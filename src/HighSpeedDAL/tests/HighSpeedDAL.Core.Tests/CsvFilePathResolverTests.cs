using Xunit;
using FluentAssertions;
using System;
using System.IO;
using HighSpeedDAL.Core.Utilities;

namespace HighSpeedDAL.Tests.Utilities;

/// <summary>
/// Comprehensive tests for CsvFilePathResolver.
/// Validates relative and absolute path resolution logic.
/// </summary>
public class CsvFilePathResolverTests
{
    private readonly string _testAppBasePath;

    public CsvFilePathResolverTests()
    {
        _testAppBasePath = AppContext.BaseDirectory;
    }

    #region Relative Path Tests

    [Fact]
    public void Resolve_RelativePath_ReturnsAbsolutePathFromAppBase()
    {
        // Arrange
        string relativePath = "Data/States.csv";

        // Act
        string result = CsvFilePathResolver.Resolve(relativePath);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain(_testAppBasePath, "should be resolved from app base directory");
        result.Should().EndWith("States.csv");
    }

    [Fact]
    public void Resolve_RelativePathWithSubfolders_ReturnsCorrectAbsolutePath()
    {
        // Arrange
        string relativePath = "Data/Reference/States.csv";

        // Act
        string result = CsvFilePathResolver.Resolve(relativePath);

        // Assert
        result.Should().Contain("Data");
        result.Should().Contain("Reference");
        result.Should().EndWith("States.csv");
        Path.IsPathRooted(result).Should().BeTrue("should return absolute path");
    }

    [Fact]
    public void Resolve_RelativePathWithBackslashes_HandlesCorrectly()
    {
        // Arrange
        string relativePath = @"Data\Reference\States.csv";

        // Act
        string result = CsvFilePathResolver.Resolve(relativePath);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        Path.IsPathRooted(result).Should().BeTrue();
    }

    #endregion

    #region Absolute Path Tests - Windows

    [Fact]
    public void Resolve_WindowsAbsolutePath_ReturnsAsIs()
    {
        // Arrange
        string absolutePath = @"C:\Data\States.csv";

        // Act
        string result = CsvFilePathResolver.Resolve(absolutePath);

        // Assert
        result.Should().Be(absolutePath, "absolute path should be used as-is");
    }

    [Fact]
    public void Resolve_WindowsUncPath_ReturnsAsIs()
    {
        // Arrange
        string uncPath = @"\\server\share\Data\States.csv";

        // Act
        string result = CsvFilePathResolver.Resolve(uncPath);

        // Assert
        result.Should().Be(uncPath, "UNC path should be used as-is");
    }

    [Theory]
    [InlineData(@"C:\Data\States.csv")]
    [InlineData(@"D:\Import\Reference\Countries.csv")]
    [InlineData(@"E:\Temp\file.csv")]
    public void Resolve_WindowsDriveLetter_RecognizedAsAbsolute(string absolutePath)
    {
        // Act
        string result = CsvFilePathResolver.Resolve(absolutePath);

        // Assert
        result.Should().Be(absolutePath);
    }

    #endregion

    #region Absolute Path Tests - Unix/Linux

    [Fact]
    public void Resolve_UnixAbsolutePath_ReturnsAsIs()
    {
        // Arrange
        string absolutePath = "/data/states.csv";

        // Act
        string result = CsvFilePathResolver.Resolve(absolutePath);

        // Assert
        result.Should().Be(absolutePath, "Unix absolute path should be used as-is");
    }

    [Theory]
    [InlineData("/data/states.csv")]
    [InlineData("/var/import/reference/countries.csv")]
    [InlineData("/tmp/file.csv")]
    public void Resolve_UnixPaths_RecognizedAsAbsolute(string absolutePath)
    {
        // Act
        string result = CsvFilePathResolver.Resolve(absolutePath);

        // Assert
        result.Should().Be(absolutePath);
    }

    #endregion

    #region File Existence Tests

    [Fact]
    public void Resolve_FileExistsAtRelativePath_ReturnsFullPath()
    {
        // Arrange
        string relativePath = "TestData/TestFile.csv";
        string fullPath = Path.Combine(_testAppBasePath, relativePath);
        
        // Create the test file
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllText(fullPath, "test");

        try
        {
            // Act
            string result = CsvFilePathResolver.Resolve(relativePath);

            // Assert
            result.Should().Be(Path.GetFullPath(fullPath));
            File.Exists(result).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            
            string directory = Path.GetDirectoryName(fullPath);
            if (Directory.Exists(directory) && Directory.GetFiles(directory).Length == 0)
            {
                Directory.Delete(directory);
            }
        }
    }

    [Fact]
    public void Resolve_FileDoesNotExistAtRelativePath_ReturnsExpectedPath()
    {
        // Arrange
        string relativePath = "Data/NonExistent.csv";

        // Act
        string result = CsvFilePathResolver.Resolve(relativePath);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        Path.IsPathRooted(result).Should().BeTrue();
        result.Should().Contain("NonExistent.csv");
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void Resolve_NullPath_ReturnsEmptyString()
    {
        // Act
        string result = CsvFilePathResolver.Resolve(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_EmptyString_ReturnsEmptyString()
    {
        // Act
        string result = CsvFilePathResolver.Resolve(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_WhitespaceOnly_ReturnsEmptyString()
    {
        // Act
        string result = CsvFilePathResolver.Resolve("   ");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_SingleFilename_TreatsAsRelative()
    {
        // Arrange
        string filename = "States.csv";

        // Act
        string result = CsvFilePathResolver.Resolve(filename);

        // Assert
        result.Should().Contain(_testAppBasePath);
        result.Should().EndWith("States.csv");
        Path.IsPathRooted(result).Should().BeTrue();
    }

    #endregion

    #region Mixed Separator Tests

    [Fact]
    public void Resolve_MixedSeparators_HandlesCorrectly()
    {
        // Arrange
        string mixedPath = @"Data/Reference\States.csv";

        // Act
        string result = CsvFilePathResolver.Resolve(mixedPath);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        Path.IsPathRooted(result).Should().BeTrue();
    }

    #endregion

    #region Special Characters Tests

    [Theory]
    [InlineData("Data/States (US).csv")]
    [InlineData("Data/States-2024.csv")]
    [InlineData("Data/States_Reference.csv")]
    public void Resolve_SpecialCharactersInPath_HandlesCorrectly(string pathWithSpecialChars)
    {
        // Act
        string result = CsvFilePathResolver.Resolve(pathWithSpecialChars);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        Path.IsPathRooted(result).Should().BeTrue();
    }

    #endregion

    #region Real-World Scenarios Tests

    [Theory]
    [InlineData("Data/States.csv", "relative path from app root")]
    [InlineData(@"C:\ImportData\States.csv", "Windows absolute path")]
    [InlineData("/var/data/states.csv", "Unix absolute path")]
    [InlineData("../Data/States.csv", "relative path with parent directory")]
    [InlineData("./Data/States.csv", "relative path with current directory")]
    public void Resolve_RealWorldScenarios_HandlesCorrectly(string inputPath, string scenario)
    {
        // Act
        string result = CsvFilePathResolver.Resolve(inputPath);

        // Assert
        result.Should().NotBeNullOrWhiteSpace(because: scenario);
        
        // For absolute paths, should return as-is or with minor normalization
        // For relative paths, should return rooted path
        Path.IsPathRooted(result).Should().BeTrue(because: "all results should be absolute paths");
    }

    #endregion
}
