ALTER TABLE [UrlTranslation] ADD COLUMN [Complete] boolean DEFAULT 0
UPDATE [UrlTranslation] SET [Complete] = 1

CREATE TABLE [Photo] (
        [Id] varchar(20) NOT NULL,
        [ShortUrl] varchar(64) NOT NULL,
        [LongUrl] varchar NOT NULL,
        [ImageUrl] varchar NOT NULL,
        [Date] integer NOT NULL,
        [StatusId] integer NOT NULL,
        [Sizes] varchar(64) NOT NULL);
CREATE INDEX Photo_StatusId_index on Photo(StatusId);