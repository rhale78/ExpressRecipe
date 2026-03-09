using ExpressRecipe.ScannerService.Data;
using FluentAssertions;

namespace ExpressRecipe.VisionService.Tests;

/// <summary>
/// Tests for the data-transfer objects used in the vision capture and
/// correction-report pipeline. These are pure DTO / default-value tests
/// that verify contracts relied on by both VisionService and ScannerService.
/// </summary>
public class CorrectionReportTests
{
    // -----------------------------------------------------------------------
    // CorrectionReportRecord
    // -----------------------------------------------------------------------

    [Fact]
    public void CorrectionReportRecord_DefaultStatus_IsPending()
    {
        CorrectionReportRecord record = new CorrectionReportRecord();

        record.Status.Should().Be("Pending",
            "new reports must start in the Pending state awaiting moderator review");
    }

    [Fact]
    public void CorrectionReportRecord_ReviewedByAndReviewedAt_AreNullByDefault()
    {
        CorrectionReportRecord record = new CorrectionReportRecord();

        record.ReviewedBy.Should().BeNull("no reviewer assigned until a moderator acts");
        record.ReviewedAt.Should().BeNull("not yet reviewed");
    }

    [Fact]
    public void CorrectionReportRecord_AiGuessAndUserCorrection_AreNullByDefault()
    {
        CorrectionReportRecord record = new CorrectionReportRecord();

        record.AiGuess.Should().BeNull();
        record.UserCorrection.Should().BeNull();
        record.UserNote.Should().BeNull();
    }

    [Fact]
    public void CorrectionReportRecord_SetAllFields_RoundTripsCorrectly()
    {
        Guid captureId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Guid reviewerId = Guid.NewGuid();
        DateTime reviewedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        CorrectionReportRecord record = new CorrectionReportRecord
        {
            Id = Guid.NewGuid(),
            VisionCaptureId = captureId,
            UserId = userId,
            AiGuess = "Cheerios",
            UserCorrection = "Honey Nut Cheerios",
            UserNote = "wrong variant",
            Status = "Resolved",
            ReviewedBy = reviewerId,
            ReviewedAt = reviewedAt,
        };

        record.AiGuess.Should().Be("Cheerios");
        record.UserCorrection.Should().Be("Honey Nut Cheerios");
        record.Status.Should().Be("Resolved");
        record.ReviewedBy.Should().Be(reviewerId);
        record.ReviewedAt.Should().Be(reviewedAt);
    }

    // -----------------------------------------------------------------------
    // VisionCaptureRecord
    // -----------------------------------------------------------------------

    [Fact]
    public void VisionCaptureRecord_DefaultCaptureImageJpeg_IsEmptyByteArray()
    {
        VisionCaptureRecord record = new VisionCaptureRecord();

        record.CaptureImageJpeg.Should().NotBeNull("property is initialised to Array.Empty<byte>()");
        record.CaptureImageJpeg.Should().BeEmpty("no image data has been assigned");
    }

    [Fact]
    public void VisionCaptureRecord_DefaultProductFoundInDb_IsFalse()
    {
        VisionCaptureRecord record = new VisionCaptureRecord();

        record.ProductFoundInDb.Should().BeFalse(
            "a fresh capture record has not yet been matched against the product database");
    }

    [Fact]
    public void VisionCaptureRecord_NullableNavigationProperties_AreNullByDefault()
    {
        VisionCaptureRecord record = new VisionCaptureRecord();

        record.ScanHistoryId.Should().BeNull();
        record.DetectedBarcode.Should().BeNull();
        record.DetectedProductName.Should().BeNull();
        record.DetectedBrand.Should().BeNull();
        record.ProviderUsed.Should().BeNull();
        record.Confidence.Should().BeNull();
        record.ResolvedProductId.Should().BeNull();
    }

    [Fact]
    public void VisionCaptureRecord_SetImageBytes_StoresCorrectly()
    {
        byte[] jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        VisionCaptureRecord record = new VisionCaptureRecord
        {
            CaptureImageJpeg = jpeg,
            DetectedProductName = "Tropicana OJ",
            ProviderUsed = "PaddleOCR",
            Confidence = 0.82m,
            ProductFoundInDb = true,
        };

        record.CaptureImageJpeg.Should().Equal(jpeg);
        record.DetectedProductName.Should().Be("Tropicana OJ");
        record.Confidence.Should().Be(0.82m);
        record.ProductFoundInDb.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // TrainingExportRow
    // -----------------------------------------------------------------------

    [Fact]
    public void TrainingExportRow_NullableStringProperties_AreNullByDefault()
    {
        TrainingExportRow row = new TrainingExportRow();

        row.DetectedBarcode.Should().BeNull();
        row.DetectedProductName.Should().BeNull();
        row.DetectedBrand.Should().BeNull();
        row.ProviderUsed.Should().BeNull();
        row.CorrectedProductName.Should().BeNull();
    }

    [Fact]
    public void TrainingExportRow_NullableDecimalConfidence_IsNullByDefault()
    {
        TrainingExportRow row = new TrainingExportRow();

        row.Confidence.Should().BeNull("confidence is only populated after a provider runs");
    }

    [Fact]
    public void TrainingExportRow_SetAllFields_RoundTripsCorrectly()
    {
        Guid captureId = Guid.NewGuid();
        DateTime capturedAt = new DateTime(2025, 5, 20, 8, 30, 0, DateTimeKind.Utc);

        TrainingExportRow row = new TrainingExportRow
        {
            CaptureId = captureId,
            DetectedBarcode = "012000030222",
            DetectedProductName = "Pepsi Cola",
            DetectedBrand = "PepsiCo",
            ProviderUsed = "OllamaVision",
            Confidence = 0.76m,
            CorrectedProductName = "Pepsi Zero Sugar",
            CapturedAt = capturedAt,
        };

        row.CaptureId.Should().Be(captureId);
        row.DetectedBarcode.Should().Be("012000030222");
        row.CorrectedProductName.Should().Be("Pepsi Zero Sugar");
        row.Confidence.Should().Be(0.76m);
        row.CapturedAt.Should().Be(capturedAt);
    }
}
