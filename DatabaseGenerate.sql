IF DB_ID('OT_Assessment_DB') IS NULL
    CREATE DATABASE OT_Assessment_DB;
GO

USE OT_Assessment_DB;
GO

IF OBJECT_ID('dbo.Player') IS NULL
CREATE TABLE dbo.Player (
    AccountId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Player PRIMARY KEY,
    Username    NVARCHAR(100)    NOT NULL,
    CreatedAt   DATETIME2(0)     NOT NULL CONSTRAINT DF_Player_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

IF OBJECT_ID('dbo.Provider') IS NULL
CREATE TABLE dbo.Provider (
    ProviderId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Provider PRIMARY KEY,
    Name        NVARCHAR(200)     NOT NULL CONSTRAINT UQ_Provider_Name UNIQUE
);
GO

IF OBJECT_ID('dbo.Game') IS NULL
CREATE TABLE dbo.Game (
    GameId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Game PRIMARY KEY,
    ProviderId  INT               NOT NULL CONSTRAINT FK_Game_Provider REFERENCES dbo.Provider(ProviderId),
    Name        NVARCHAR(200)     NOT NULL,
    Theme       NVARCHAR(100)     NULL,
    CONSTRAINT UQ_Game_Provider_Name UNIQUE (ProviderId, Name)
);
GO

IF OBJECT_ID('dbo.CasinoWager') IS NULL
CREATE TABLE dbo.CasinoWager (
    WagerId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CasinoWager PRIMARY KEY NONCLUSTERED,
    AccountId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_CasinoWager_Player REFERENCES dbo.Player(AccountId),
    GameId               INT              NOT NULL CONSTRAINT FK_CasinoWager_Game REFERENCES dbo.Game(GameId),
    BrandId              UNIQUEIDENTIFIER NOT NULL,
    TransactionId        UNIQUEIDENTIFIER NOT NULL,
    ExternalReferenceId  UNIQUEIDENTIFIER NOT NULL,
    TransactionTypeId    UNIQUEIDENTIFIER NOT NULL,
    Amount               DECIMAL(18,2)    NOT NULL,
    NumberOfBets         INT              NOT NULL,
    CountryCode          CHAR(2)          NOT NULL,
    DurationMs           BIGINT           NOT NULL,
    CreatedDateTime      DATETIMEOFFSET(3) NOT NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CasinoWager_Account_Created')
CREATE CLUSTERED INDEX IX_CasinoWager_Account_Created
    ON dbo.CasinoWager (AccountId, CreatedDateTime DESC);
GO

CREATE OR ALTER PROCEDURE dbo.usp_InsertCasinoWager
    @WagerId             UNIQUEIDENTIFIER,
    @AccountId           UNIQUEIDENTIFIER,
    @Username            NVARCHAR(100),
    @Provider            NVARCHAR(200),
    @GameName            NVARCHAR(200),
    @Theme               NVARCHAR(100),
    @BrandId             UNIQUEIDENTIFIER,
    @TransactionId       UNIQUEIDENTIFIER,
    @ExternalReferenceId UNIQUEIDENTIFIER,
    @TransactionTypeId   UNIQUEIDENTIFIER,
    @Amount              DECIMAL(18,2),
    @NumberOfBets        INT,
    @CountryCode         CHAR(2),
    @DurationMs          BIGINT,
    @CreatedDateTime     DATETIMEOFFSET(3)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.CasinoWager WHERE WagerId = @WagerId) RETURN;

    DECLARE @ProviderId INT, @GameId INT;

    SELECT @ProviderId = ProviderId FROM dbo.Provider WHERE Name = @Provider;
    IF @ProviderId IS NULL
    BEGIN
        INSERT dbo.Provider (Name) VALUES (@Provider);
        SET @ProviderId = SCOPE_IDENTITY();
    END

    SELECT @GameId = GameId FROM dbo.Game WHERE ProviderId = @ProviderId AND Name = @GameName;
    IF @GameId IS NULL
    BEGIN
        INSERT dbo.Game (ProviderId, Name, Theme) VALUES (@ProviderId, @GameName, @Theme);
        SET @GameId = SCOPE_IDENTITY();
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.Player WHERE AccountId = @AccountId)
        INSERT dbo.Player (AccountId, Username) VALUES (@AccountId, @Username);

    INSERT dbo.CasinoWager
        (WagerId, AccountId, GameId, BrandId, TransactionId, ExternalReferenceId,
         TransactionTypeId, Amount, NumberOfBets, CountryCode, DurationMs, CreatedDateTime)
    VALUES
        (@WagerId, @AccountId, @GameId, @BrandId, @TransactionId, @ExternalReferenceId,
         @TransactionTypeId, @Amount, @NumberOfBets, @CountryCode, @DurationMs, @CreatedDateTime);
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_GetPlayerWagers
    @AccountId UNIQUEIDENTIFIER,
    @Page      INT,
    @PageSize  INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT w.WagerId, g.Name AS Game, p.Name AS Provider, w.Amount, w.CreatedDateTime AS CreatedDate
    FROM dbo.CasinoWager w
    JOIN dbo.Game g     ON g.GameId = w.GameId
    JOIN dbo.Provider p ON p.ProviderId = g.ProviderId
    WHERE w.AccountId = @AccountId
    ORDER BY w.CreatedDateTime DESC
    OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) AS Total FROM dbo.CasinoWager WHERE AccountId = @AccountId;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_GetTopSpenders
    @Count INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Count) pl.AccountId, pl.Username, SUM(w.Amount) AS TotalAmountSpend
    FROM dbo.CasinoWager w
    JOIN dbo.Player pl ON pl.AccountId = w.AccountId
    GROUP BY pl.AccountId, pl.Username
    ORDER BY TotalAmountSpend DESC;
END
GO