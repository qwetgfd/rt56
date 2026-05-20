/*
  WARNING: Drops all SharePoint plugin procedures and tables (all application data).
  Production / fresh install: run in order: 01_tables.sql, 02_seed.sql, 03_procedures.sql
*/

IF OBJECT_ID('dbo.sel_applicationtypes', 'P') IS NOT NULL DROP PROCEDURE dbo.sel_applicationtypes;
IF OBJECT_ID('dbo.sel_application', 'P') IS NOT NULL DROP PROCEDURE dbo.sel_application;
IF OBJECT_ID('dbo.sel_applicationbyid', 'P') IS NOT NULL DROP PROCEDURE dbo.sel_applicationbyid;
IF OBJECT_ID('dbo.commit_application', 'P') IS NOT NULL DROP PROCEDURE dbo.commit_application;
IF OBJECT_ID('dbo.commit_applicationdelete', 'P') IS NOT NULL DROP PROCEDURE dbo.commit_applicationdelete;
GO

IF OBJECT_ID('dbo.di_application', 'U') IS NOT NULL DROP TABLE dbo.di_application;
IF OBJECT_ID('dbo.di_applicationtype', 'U') IS NOT NULL DROP TABLE dbo.di_applicationtype;
GO

CREATE TABLE dbo.di_applicationtype
(
    applicationtypeid     INT             IDENTITY(1,1) NOT NULL PRIMARY KEY CLUSTERED,
    code                  VARCHAR(32)     NOT NULL,
    displayname           NVARCHAR(64)    NOT NULL,
    description           NVARCHAR(256)   NULL,
    isactive              BIT             NOT NULL DEFAULT (1),
    createdon             DATETIME2(3)    NOT NULL DEFAULT (GETUTCDATE())
);
GO

CREATE UNIQUE INDEX ix_uq_di_applicationtype_code ON dbo.di_applicationtype (code);
GO

CREATE TABLE dbo.di_application
(
    applicationid         UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWID()) PRIMARY KEY CLUSTERED,
    applicationtypeid     INT             NOT NULL,
    ownerkey              NVARCHAR(128)   NOT NULL DEFAULT (N'system'),
    displayname           NVARCHAR(128)   NOT NULL,
    tenantid              NVARCHAR(64)    NOT NULL,
    clientid              NVARCHAR(64)    NOT NULL,
    clientsecret          NVARCHAR(512)   NOT NULL,
    hostname              NVARCHAR(256)   NOT NULL,
    sitename              NVARCHAR(256)   NULL,
    libraryname           NVARCHAR(256)   NULL,
    consumerclientid      NVARCHAR(64)    NULL,
    consumersecret        NVARCHAR(512)   NULL,
    notes                 NVARCHAR(1024)  NULL,
    isactive              BIT             NOT NULL DEFAULT (1),
    createdon             DATETIME2(3)    NOT NULL DEFAULT (GETUTCDATE()),
    modifiedon            DATETIME2(3)    NULL
);
GO

CREATE INDEX ix_di_application_ownerkey ON dbo.di_application (ownerkey);
CREATE INDEX ix_di_application_typeid ON dbo.di_application (applicationtypeid);
GO
