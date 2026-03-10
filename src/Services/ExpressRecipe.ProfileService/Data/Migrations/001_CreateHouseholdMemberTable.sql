IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HouseholdMember')
BEGIN
CREATE TABLE HouseholdMember (
    Id                 UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId        UNIQUEIDENTIFIER NOT NULL,
    MemberType         NVARCHAR(50) NOT NULL,
    DisplayName        NVARCHAR(100) NOT NULL,
    BirthYear          SMALLINT NULL,
    LinkedUserId       UNIQUEIDENTIFIER NULL,
    HasUserAccount     BIT NOT NULL DEFAULT 0,
    IsGuest            BIT NOT NULL DEFAULT 0,
    GuestSubtype       NVARCHAR(50) NULL,
    GuestExpiresAt     DATETIME2 NULL,
    SourceHouseholdId  UNIQUEIDENTIFIER NULL,
    CreatedAt          DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy          UNIQUEIDENTIFIER NULL,
    UpdatedAt          DATETIME2 NULL,
    IsDeleted          BIT NOT NULL DEFAULT 0,
    RowVersion         ROWVERSION,
    CONSTRAINT CK_HouseholdMember_Type CHECK (MemberType IN (
        'FamilyAdmin','Adult','Teen','Child','Infant','GeneralUser',
        'TemporaryVisitor','RecurringGuest','CrossHouseholdGuest'))
);
CREATE INDEX IX_HouseholdMember_HouseholdId ON HouseholdMember(HouseholdId) WHERE IsDeleted = 0;
END
