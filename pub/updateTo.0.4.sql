CREATE TABLE [LastId] (
      [Name] varchar(64) NOT NULL,
      [ItemId] integer NOT NULL)
      
INSERT INTO LastId(Name, ItemId) Values ('timeline', (select TimelineId from AppState))
INSERT INTO LastId(Name, ItemId) Values ('mentions', (select MentionsId from AppState))

DROP TABLE AppState