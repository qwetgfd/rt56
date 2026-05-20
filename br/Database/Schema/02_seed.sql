MERGE dbo.di_applicationtype AS target
USING (VALUES
    ('tp_internal', N'TP-Internal', N'Preconfigured internal application.'),
    ('tp_external', N'External', N'External application.'),
    ('custom',      N'Custom',      N'Custom Azure AD application.')
) AS source (code, displayname, description)
ON target.code = source.code
WHEN MATCHED THEN
    UPDATE SET displayname = source.displayname,
               description  = source.description,
               isactive    = 1
WHEN NOT MATCHED THEN
    INSERT (code, displayname, description) VALUES (source.code, source.displayname, source.description);
GO
