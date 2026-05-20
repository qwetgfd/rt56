IF OBJECT_ID('dbo.sel_applicationtypes', 'P') IS NOT NULL DROP PROCEDURE dbo.sel_applicationtypes;
IF OBJECT_ID('dbo.sel_application', 'P') IS NOT NULL DROP PROCEDURE dbo.sel_application;
IF OBJECT_ID('dbo.sel_applicationbyid', 'P') IS NOT NULL DROP PROCEDURE dbo.sel_applicationbyid;
IF OBJECT_ID('dbo.commit_application', 'P') IS NOT NULL DROP PROCEDURE dbo.commit_application;
IF OBJECT_ID('dbo.commit_applicationdelete', 'P') IS NOT NULL DROP PROCEDURE dbo.commit_applicationdelete;
GO

CREATE PROCEDURE dbo.sel_applicationtypes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  applicationtypeid   AS ApplicationTypeId,
            code                AS Code,
            displayname         AS DisplayName,
            description         AS Description
    FROM    dbo.di_applicationtype
    WHERE   isactive = 1
    ORDER BY applicationtypeid;
END;
GO

CREATE PROCEDURE dbo.sel_application
    @Ownerkey           NVARCHAR(128) = NULL,
    @Applicationtypeid  INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  a.applicationid       AS ApplicationId,
            a.applicationtypeid   AS ApplicationTypeId,
            t.code                AS ApplicationTypeCode,
            t.displayname         AS ApplicationTypeName,
            a.ownerkey            AS OwnerKey,
            a.displayname         AS DisplayName,
            a.tenantid            AS TenantId,
            a.clientid            AS ClientId,
            a.clientsecret        AS ClientSecret,
            a.hostname            AS HostName,
            a.sitename            AS SiteName,
            a.libraryname         AS LibraryName,
            a.consumerclientid    AS ConsumerClientId,
            a.consumersecret      AS ConsumerSecret,
            a.notes               AS Notes,
            a.isactive            AS IsActive,
            a.createdon           AS CreatedOn,
            a.modifiedon          AS ModifiedOn
    FROM    dbo.di_application AS a
            INNER JOIN dbo.di_applicationtype AS t
                ON t.applicationtypeid = a.applicationtypeid
    WHERE   a.isactive = 1
      AND   (@Ownerkey IS NULL OR a.ownerkey = @Ownerkey)
      AND   (@Applicationtypeid IS NULL OR a.applicationtypeid = @Applicationtypeid)
    ORDER BY a.displayname;
END;
GO

CREATE PROCEDURE dbo.sel_applicationbyid
    @Applicationid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  a.applicationid       AS ApplicationId,
            a.applicationtypeid   AS ApplicationTypeId,
            t.code                AS ApplicationTypeCode,
            t.displayname         AS ApplicationTypeName,
            a.ownerkey            AS OwnerKey,
            a.displayname         AS DisplayName,
            a.tenantid            AS TenantId,
            a.clientid            AS ClientId,
            a.clientsecret        AS ClientSecret,
            a.hostname            AS HostName,
            a.sitename            AS SiteName,
            a.libraryname         AS LibraryName,
            a.consumerclientid    AS ConsumerClientId,
            a.consumersecret      AS ConsumerSecret,
            a.notes               AS Notes,
            a.isactive            AS IsActive,
            a.createdon           AS CreatedOn,
            a.modifiedon          AS ModifiedOn
    FROM    dbo.di_application AS a
            INNER JOIN dbo.di_applicationtype AS t
                ON t.applicationtypeid = a.applicationtypeid
    WHERE   a.applicationid = @Applicationid
      AND   a.isactive = 1;
END;
GO

CREATE PROCEDURE dbo.commit_application
    @Applicationid      UNIQUEIDENTIFIER = NULL OUTPUT,
    @Applicationtypeid  INT,
    @Ownerkey           NVARCHAR(128),
    @Displayname        NVARCHAR(128),
    @Tenantid           NVARCHAR(64),
    @Clientid           NVARCHAR(64),
    @Clientsecret       NVARCHAR(512),
    @Hostname           NVARCHAR(256),
    @Sitename           NVARCHAR(256) = NULL,
    @Libraryname        NVARCHAR(256) = NULL,
    @Consumerclientid   NVARCHAR(64) = NULL,
    @Consumersecret     NVARCHAR(512) = NULL,
    @Notes              NVARCHAR(1024) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @Applicationid IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.di_application WHERE applicationid = @Applicationid)
    BEGIN
        IF @Applicationid IS NULL SET @Applicationid = NEWID();
        IF @Consumerclientid IS NULL OR LTRIM(RTRIM(@Consumerclientid)) = N''
            SET @Consumerclientid = CONVERT(NVARCHAR(36), NEWID());
        IF @Consumersecret IS NULL OR LTRIM(RTRIM(@Consumersecret)) = N''
            SET @Consumersecret = LOWER(CONVERT(NVARCHAR(64), CRYPT_GEN_RANDOM(32), 2));
        INSERT INTO dbo.di_application
            (applicationid, applicationtypeid, ownerkey, displayname,
             tenantid, clientid, clientsecret, hostname, sitename, libraryname,
             consumerclientid, consumersecret, notes)
        VALUES
            (@Applicationid, @Applicationtypeid, @Ownerkey, @Displayname,
             @Tenantid, @Clientid, @Clientsecret, @Hostname, @Sitename, @Libraryname,
             @Consumerclientid, @Consumersecret, @Notes);
    END
    ELSE
    BEGIN
        UPDATE  dbo.di_application
        SET     applicationtypeid = @Applicationtypeid,
                ownerkey          = @Ownerkey,
                displayname       = @Displayname,
                tenantid          = @Tenantid,
                clientid          = @Clientid,
                clientsecret      = @Clientsecret,
                hostname          = @Hostname,
                sitename          = @Sitename,
                libraryname       = @Libraryname,
                consumerclientid  = CASE
                    WHEN @Consumerclientid IS NOT NULL AND LTRIM(RTRIM(@Consumerclientid)) <> N'' THEN @Consumerclientid
                    ELSE consumerclientid END,
                consumersecret    = CASE
                    WHEN @Consumersecret IS NOT NULL AND LTRIM(RTRIM(@Consumersecret)) <> N'' THEN @Consumersecret
                    ELSE consumersecret END,
                notes             = @Notes,
                modifiedon        = GETUTCDATE()
        WHERE   applicationid = @Applicationid;
    END
    EXEC dbo.sel_applicationbyid @Applicationid;
END;
GO

CREATE PROCEDURE dbo.commit_applicationdelete
    @Applicationid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE  dbo.di_application
    SET     isactive   = 0,
            modifiedon = GETUTCDATE()
    WHERE   applicationid = @Applicationid;
    SELECT @@ROWCOUNT AS AffectedRows;
END;
GO
