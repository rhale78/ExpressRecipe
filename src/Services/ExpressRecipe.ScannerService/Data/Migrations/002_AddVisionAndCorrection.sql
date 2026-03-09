-- Migration: 002_AddVisionAndCorrection
-- Description: Add vision capture tracking, correction reports, and scan mode metadata

ALTER TABLE ScanHistory
    ADD ScanMode           NVARCHAR(50)  NOT NULL DEFAULT 'UPC',
        EntryPoint         NVARCHAR(50)  NULL,
        SessionMode        NVARCHAR(20)  NOT NULL DEFAULT 'Single',
        VisionCaptureId    UNIQUEIDENTIFIER NULL,
        DetectionProvider  NVARCHAR(100) NULL;
GO

CREATE TABLE VisionCapture (
    Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId              UNIQUEIDENTIFIER NOT NULL,
    ScanHistoryId       UNIQUEIDENTIFIER NULL,
    CaptureImageJpeg    VARBINARY(MAX) NOT NULL,
    DetectedBarcode     NVARCHAR(100) NULL,
    DetectedProductName NVARCHAR(300) NULL,
    DetectedBrand       NVARCHAR(200) NULL,
    ProviderUsed        NVARCHAR(100) NULL,
    Confidence          DECIMAL(5,4) NULL,
    ProductFoundInDb    BIT NOT NULL DEFAULT 0,
    ResolvedProductId   UNIQUEIDENTIFIER NULL,
    IsTrainingData      BIT NOT NULL DEFAULT 0,
    CapturedAt          DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_VisionCapture_UserId    ON VisionCapture(UserId);
CREATE INDEX IX_VisionCapture_CapturedAt ON VisionCapture(CapturedAt DESC);
GO

CREATE TABLE CorrectionReport (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    VisionCaptureId UNIQUEIDENTIFIER NOT NULL,
    UserId          UNIQUEIDENTIFIER NOT NULL,
    AiGuess         NVARCHAR(300) NULL,
    UserCorrection  NVARCHAR(300) NULL,
    UserNote        NVARCHAR(1000) NULL,
    Status          NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    ReviewedBy      UNIQUEIDENTIFIER NULL,
    ReviewedAt      DATETIME2 NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_CorrectionReport_Capture FOREIGN KEY (VisionCaptureId) REFERENCES VisionCapture(Id),
    CONSTRAINT CK_CorrectionReport_Status CHECK (Status IN ('Pending','Approved','Rejected'))
);
CREATE INDEX IX_CorrectionReport_Status ON CorrectionReport(Status, CreatedAt DESC);
GO
