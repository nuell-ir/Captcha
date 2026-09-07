-- Migration: 001_CreateCaptchaCodesTable.sql
-- Creates the CaptchaCodes table and the expiration index on CreationDate.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CaptchaCodes' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.CaptchaCodes (
        Id bigint NOT NULL,
        Captcha int NOT NULL,
        CreationDate datetime2 NOT NULL,
        CONSTRAINT PK_CaptchaCodes PRIMARY KEY CLUSTERED (Id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CaptchaCodes_CreationDate' AND object_id = OBJECT_ID('dbo.CaptchaCodes'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_CaptchaCodes_CreationDate ON dbo.CaptchaCodes (CreationDate);
END;

