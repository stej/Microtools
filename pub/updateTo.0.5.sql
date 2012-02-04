ALTER TABLE [UrlTranslation] ADD COLUMN [Complete] boolean DEFAULT 0
UPDATE [UrlTranslation] SET [Complete] = 1