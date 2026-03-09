using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ExpressRecipe.VisionService.Controllers;
using ExpressRecipe.VisionService.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;

namespace ExpressRecipe.VisionService.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="VisionController"/>.
/// The controller is exercised directly (no HTTP pipeline) by supplying a configured
/// <see cref="DefaultHttpContext"/> for each scenario.
/// </summary>
public class VisionControllerTests
{
    private readonly Mock<IVisionService> _serviceMock = new Mock<IVisionService>();
    private readonly Guid _userId = Guid.NewGuid();

    private VisionController CreateController(DefaultHttpContext httpContext)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, _userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);

        var controller = new VisionController(
            _serviceMock.Object,
            NullLogger<VisionController>.Instance);

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    // -----------------------------------------------------------------------
    // Helper: build an HttpContext with a JSON body
    // -----------------------------------------------------------------------

    private static DefaultHttpContext JsonContext(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var ctx = new DefaultHttpContext();
        ctx.Request.ContentType = "application/json";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return ctx;
    }

    // -----------------------------------------------------------------------
    // Helper: build an HttpContext with a multipart/form-data "image" field
    // -----------------------------------------------------------------------

    private static DefaultHttpContext MultipartContext(byte[] imageBytes)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Name).Returns("image");
        mockFile.Setup(f => f.Length).Returns(imageBytes.Length);
        mockFile
            .Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<Stream, CancellationToken>((stream, _) =>
            {
                stream.Write(imageBytes);
                return Task.CompletedTask;
            });

        var files = new FormFileCollection { mockFile.Object };
        var form = new FormCollection(new Dictionary<string, StringValues>(), files);

        var ctx = new DefaultHttpContext();
        ctx.Request.ContentType = "multipart/form-data; boundary=----TestBoundary";
        ctx.Request.Form = form;
        return ctx;
    }

    // -----------------------------------------------------------------------
    // Analyze — bad request cases
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Analyze_WithNoImage_ReturnsBadRequest()
    {
        // Arrange: no content type, no body
        var controller = CreateController(new DefaultHttpContext());

        // Act
        IActionResult result = await controller.Analyze(CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>("no image data was supplied");
        _serviceMock.Verify(
            s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<VisionOptions>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "the service must not be called when there is nothing to analyze");
    }

    [Fact]
    public async Task Analyze_WithInvalidBase64_ReturnsBadRequest()
    {
        // Arrange: JSON body whose base64 string cannot be decoded
        var httpContext = JsonContext(new VisionAnalyzeRequest { Base64Image = "!!!not-valid-base64!!!" });
        var controller = CreateController(httpContext);

        // Act
        IActionResult result = await controller.Analyze(CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>(
            "the controller must reject a request whose base64 payload cannot be decoded");
        _serviceMock.Verify(
            s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<VisionOptions>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "the service must not be called for an invalid base64 payload");
    }

    // -----------------------------------------------------------------------
    // Analyze — success cases
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Analyze_WithJsonBase64Image_ReturnsOkWithVisionResult()
    {
        // Arrange
        var expected = new VisionResult
        {
            Success = true,
            ProductName = "Cheerios",
            Brand = "General Mills",
            Confidence = 0.9,
            ProviderUsed = "OllamaVision",
            DetectedText = ["Cheerios", "Whole Grain"],
            Labels = ["cereal"],
        };
        _serviceMock
            .Setup(s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<VisionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var httpContext = JsonContext(new VisionAnalyzeRequest
        {
            Base64Image = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
        });
        var controller = CreateController(httpContext);

        // Act
        IActionResult result = await controller.Analyze(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeEquivalentTo(expected);
        _serviceMock.Verify(
            s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<VisionOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Analyze_WithJsonBase64ImageAndOptions_ForwardsOptionsToService()
    {
        // Arrange: caller explicitly disables Ollama and sets a higher confidence threshold
        var expected = new VisionResult { Success = true, ProviderUsed = "ONNX", Confidence = 0.85 };
        VisionOptions? capturedOptions = null;
        _serviceMock
            .Setup(s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<VisionOptions>(), It.IsAny<CancellationToken>()))
            .Callback<byte[], VisionOptions, CancellationToken>((_, opts, _) => capturedOptions = opts)
            .ReturnsAsync(expected);

        var httpContext = JsonContext(new VisionAnalyzeRequest
        {
            Base64Image = Convert.ToBase64String(new byte[] { 5, 6, 7 }),
            Options = new VisionOptions
            {
                AllowOnnx = true,
                AllowPaddleOcr = false,
                AllowOllamaVision = false,
                AllowAzureVision = false,
                MinConfidence = 0.75,
            },
        });
        var controller = CreateController(httpContext);

        // Act
        IActionResult result = await controller.Analyze(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        capturedOptions.Should().NotBeNull();
        capturedOptions!.AllowOllamaVision.Should().BeFalse("caller opted out of Ollama");
        capturedOptions.MinConfidence.Should().Be(0.75);
    }

    [Fact]
    public async Task Analyze_WithMultipartFormData_ReturnsOkWithVisionResult()
    {
        // Arrange
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00 }; // fake JPEG header
        var expected = new VisionResult
        {
            Success = true,
            ProductName = "Test Product",
            Confidence = 0.8,
            ProviderUsed = "ONNX",
        };
        _serviceMock
            .Setup(s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<VisionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(MultipartContext(imageBytes));

        // Act
        IActionResult result = await controller.Analyze(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeEquivalentTo(expected);
        _serviceMock.Verify(
            s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<VisionOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -----------------------------------------------------------------------
    // ExtractText — bad request cases
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractText_WithNoImage_ReturnsBadRequest()
    {
        // Arrange: no content type, no body
        var controller = CreateController(new DefaultHttpContext());

        // Act
        IActionResult result = await controller.ExtractText(CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>("no image data was supplied");
        _serviceMock.Verify(
            s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // -----------------------------------------------------------------------
    // ExtractText — success cases
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractText_WithValidBase64Image_ReturnsOkWithVisionResult()
    {
        // Arrange
        var expected = new VisionResult
        {
            Success = true,
            DetectedText = ["Cheerios", "General Mills", "Whole Grain"],
            ProviderUsed = "PaddleOCR",
            Confidence = 0.95,
        };
        _serviceMock
            .Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var httpContext = JsonContext(new VisionAnalyzeRequest
        {
            Base64Image = Convert.ToBase64String(new byte[] { 10, 20, 30 }),
        });
        var controller = CreateController(httpContext);

        // Act
        IActionResult result = await controller.ExtractText(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(expected);
        _serviceMock.Verify(
            s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExtractText_WithMultipartFormData_ReturnsOkWithVisionResult()
    {
        // Arrange
        var imageBytes = new byte[] { 1, 2, 3, 4, 5 };
        var expected = new VisionResult
        {
            Success = true,
            DetectedText = ["BEST BY 2025", "LOT A123"],
            ProviderUsed = "PaddleOCR",
        };
        _serviceMock
            .Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(MultipartContext(imageBytes));

        // Act
        IActionResult result = await controller.ExtractText(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeEquivalentTo(expected);
    }

    // -----------------------------------------------------------------------
    // Health — no authentication required
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Health_ReturnsOkWithStatus()
    {
        // Arrange: Health is [AllowAnonymous] — supply an unauthenticated context
        var status = new VisionHealthStatus
        {
            OnnxAvailable = true,
            PaddleOcrAvailable = false,
            OllamaVisionAvailable = true,
            AzureVisionAvailable = false,
            OnnxModelPath = "/models/product.onnx",
        };
        _serviceMock
            .Setup(s => s.GetHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var controller = new VisionController(
            _serviceMock.Object,
            NullLogger<VisionController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        // Act
        IActionResult result = await controller.Health(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeEquivalentTo(status);
        _serviceMock.Verify(s => s.GetHealthAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Health_ServiceUnavailable_StillReturnsOkWithFalseFlags()
    {
        // Arrange: all providers report unavailable — the controller should still return 200
        var status = new VisionHealthStatus
        {
            OnnxAvailable = false,
            PaddleOcrAvailable = false,
            OllamaVisionAvailable = false,
            AzureVisionAvailable = false,
        };
        _serviceMock
            .Setup(s => s.GetHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var controller = new VisionController(
            _serviceMock.Object,
            NullLogger<VisionController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        // Act
        IActionResult result = await controller.Health(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>(
            "the health endpoint must always return 200 so liveness probes can parse the payload");
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(status);
    }
}
