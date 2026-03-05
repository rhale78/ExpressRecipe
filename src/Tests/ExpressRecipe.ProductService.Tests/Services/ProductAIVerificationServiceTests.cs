using ExpressRecipe.ProductService.Data;
using ExpressRecipe.ProductService.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.ProductService.Tests.Services;

public class ProductAIVerificationServiceTests
{
    private readonly ProductAIVerificationService _service;

    public ProductAIVerificationServiceTests()
    {
        var logger = new Mock<ILogger<ProductAIVerificationService>>();
        _service = new ProductAIVerificationService(logger.Object);
    }

    [Fact]
    public async Task VerifyProduct_WithValidProduct_ReturnsIsValid()
    {
        // Arrange
        var product = new StagedProduct
        {
            ExternalId = "3017620422003",
            Barcode = "3017620422003",
            ProductName = "Nutella",
            IngredientsText = "Sugar, palm oil, hazelnuts, cocoa, skim milk powder"
        };

        // Act
        var (isValid, notes) = await _service.VerifyProductAsync(product);

        // Assert
        isValid.Should().BeTrue();
        notes.Should().BeNull();
    }

    [Fact]
    public async Task VerifyProduct_WithMissingProductName_ReturnsInvalid()
    {
        // Arrange
        var product = new StagedProduct
        {
            ExternalId = "3017620422003",
            Barcode = "3017620422003",
            ProductName = null
        };

        // Act
        var (isValid, notes) = await _service.VerifyProductAsync(product);

        // Assert
        isValid.Should().BeFalse();
        notes.Should().Contain("Missing product name");
    }

    [Fact]
    public async Task VerifyProduct_WithMissingExternalId_ReturnsInvalid()
    {
        // Arrange
        var product = new StagedProduct
        {
            ExternalId = string.Empty,
            ProductName = "Test Product"
        };

        // Act
        var (isValid, notes) = await _service.VerifyProductAsync(product);

        // Assert
        isValid.Should().BeFalse();
        notes.Should().Contain("Missing external ID");
    }

    [Fact]
    public async Task VerifyProduct_WithInvalidBarcodeLength_ReturnsInvalid()
    {
        // Arrange
        var product = new StagedProduct
        {
            ExternalId = "12345",
            ProductName = "Test",
            Barcode = "123" // Too short
        };

        // Act
        var (isValid, notes) = await _service.VerifyProductAsync(product);

        // Assert
        isValid.Should().BeFalse();
        notes.Should().Contain("Barcode length");
    }

    [Fact]
    public async Task VerifyProduct_WithValidUPCA_ReturnsValid()
    {
        // Arrange
        var product = new StagedProduct
        {
            ExternalId = "012345678905",
            ProductName = "Test Product",
            Barcode = "012345678905" // 12 digits - valid UPC-A
        };

        // Act
        var (isValid, notes) = await _service.VerifyProductAsync(product);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyProduct_WithEncodingArtifacts_ReturnsInvalid()
    {
        // Arrange
        var product = new StagedProduct
        {
            ExternalId = "test123",
            ProductName = "Test Product",
            IngredientsText = "SucreÃ, huile de palme ï¿½ etc" // Broken encoding
        };

        // Act
        var (isValid, notes) = await _service.VerifyProductAsync(product);

        // Assert
        isValid.Should().BeFalse();
        notes.Should().Contain("encoding");
    }

    [Fact]
    public async Task VerifyProduct_WithNullBarcode_ValidatesOtherFieldsOnly()
    {
        // Arrange
        var product = new StagedProduct
        {
            ExternalId = "test-123",
            ProductName = "Valid Product",
            Barcode = null // No barcode is fine
        };

        // Act
        var (isValid, notes) = await _service.VerifyProductAsync(product);

        // Assert
        isValid.Should().BeTrue();
        notes.Should().BeNull();
    }
}
