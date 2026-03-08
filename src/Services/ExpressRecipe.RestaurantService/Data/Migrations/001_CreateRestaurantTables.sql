-- Restaurant table (enhanced schema)
CREATE TABLE Restaurant (
    Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name             NVARCHAR(200) NOT NULL,
    Brand            NVARCHAR(200) NULL,
    Description      NVARCHAR(MAX) NULL,
    CuisineType      NVARCHAR(100) NULL,
    RestaurantType   NVARCHAR(50) NOT NULL DEFAULT 'SitDown',
    Address          NVARCHAR(500) NULL,
    City             NVARCHAR(100) NULL,
    State            NVARCHAR(50) NULL,
    ZipCode          NVARCHAR(20) NULL,
    Country          NVARCHAR(100) NULL DEFAULT 'US',
    Latitude         DECIMAL(10,7) NULL,
    Longitude        DECIMAL(10,7) NULL,
    PhoneNumber      NVARCHAR(20) NULL,
    Website          NVARCHAR(500) NULL,
    ImageUrl         NVARCHAR(500) NULL,
    PriceRange       NVARCHAR(10) NULL,
    IsChain          BIT NOT NULL DEFAULT 0,
    ChainBrandId     UNIQUEIDENTIFIER NULL,
    OsmId            BIGINT NULL,
    GersId           NVARCHAR(100) NULL,
    ExternalId       NVARCHAR(200) NULL,
    DataSource       NVARCHAR(100) NULL,
    ApprovalStatus   NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    ApprovedBy       UNIQUEIDENTIFIER NULL,
    ApprovedAt       DATETIME2 NULL,
    SubmittedBy      UNIQUEIDENTIFIER NULL,
    AverageRating    DECIMAL(3,2) NULL,
    RatingCount      INT NOT NULL DEFAULT 0,
    IsActive         BIT NOT NULL DEFAULT 1,
    CreatedAt        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy        UNIQUEIDENTIFIER NULL,
    UpdatedAt        DATETIME2 NULL,
    UpdatedBy        UNIQUEIDENTIFIER NULL,
    CONSTRAINT CK_Restaurant_ApprovalStatus CHECK (ApprovalStatus IN ('Pending','Approved','Rejected')),
    CONSTRAINT CK_Restaurant_RestaurantType CHECK (RestaurantType IN ('FastFood','SitDown','FastCasual','Cafe','Bakery','FoodTruck'))
);
GO

CREATE INDEX IX_Restaurant_City_State   ON Restaurant(City, State) WHERE IsActive = 1;
CREATE INDEX IX_Restaurant_Lat_Lon      ON Restaurant(Latitude, Longitude) WHERE IsActive = 1;
CREATE INDEX IX_Restaurant_Chain        ON Restaurant(ChainBrandId);
CREATE INDEX IX_Restaurant_ExternalId   ON Restaurant(ExternalId, DataSource);
CREATE INDEX IX_Restaurant_Name         ON Restaurant(Name) WHERE IsActive = 1;
CREATE INDEX IX_Restaurant_CuisineType  ON Restaurant(CuisineType) WHERE IsActive = 1;
CREATE INDEX IX_Restaurant_ApprovalStatus ON Restaurant(ApprovalStatus) WHERE IsActive = 1;
CREATE UNIQUE INDEX IX_Restaurant_OsmId ON Restaurant(OsmId) WHERE OsmId IS NOT NULL;
GO

-- Restaurant Hours
CREATE TABLE RestaurantHours (
    Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RestaurantId UNIQUEIDENTIFIER NOT NULL,
    DayOfWeek    TINYINT NOT NULL,
    OpenTime     TIME NULL,
    CloseTime    TIME NULL,
    IsClosed     BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_RestaurantHours_Restaurant FOREIGN KEY (RestaurantId) REFERENCES Restaurant(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_RestaurantHours UNIQUE (RestaurantId, DayOfWeek)
);
GO

-- Restaurant Chain
CREATE TABLE RestaurantChain (
    Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CanonicalName NVARCHAR(200) NOT NULL UNIQUE,
    Aliases       NVARCHAR(MAX) NULL,
    LogoUrl       NVARCHAR(500) NULL,
    Website       NVARCHAR(500) NULL,
    IsNational    BIT NOT NULL DEFAULT 1,
    CreatedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

-- Restaurant Import Log
CREATE TABLE RestaurantImportLog (
    Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    DataSource       NVARCHAR(100) NOT NULL,
    ImportedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    RecordsProcessed INT NOT NULL DEFAULT 0,
    RecordsImported  INT NOT NULL DEFAULT 0,
    RecordsUpdated   INT NOT NULL DEFAULT 0,
    ErrorCount       INT NOT NULL DEFAULT 0,
    Success          BIT NOT NULL DEFAULT 0
);
GO

-- User Restaurant Ratings
CREATE TABLE UserRestaurantRating (
    Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId       UNIQUEIDENTIFIER NOT NULL,
    RestaurantId UNIQUEIDENTIFIER NOT NULL,
    Rating       INT NOT NULL CHECK (Rating >= 1 AND Rating <= 5),
    Review       NVARCHAR(MAX) NULL,
    VisitDate    DATETIME2 NULL,
    CreatedAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt    DATETIME2 NULL,
    CONSTRAINT FK_UserRestaurantRating_Restaurant FOREIGN KEY (RestaurantId) REFERENCES Restaurant(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_UserRestaurantRating_User_Restaurant UNIQUE (UserId, RestaurantId)
);
GO

CREATE INDEX IX_UserRestaurantRating_UserId       ON UserRestaurantRating(UserId);
CREATE INDEX IX_UserRestaurantRating_RestaurantId ON UserRestaurantRating(RestaurantId);
GO
