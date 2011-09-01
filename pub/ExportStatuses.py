import System.IO
exportFile = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, 'statuses.export.db')
if not System.IO.File.Exists(exportFile):
	raise Exception('File ' + exportFile + ' does not exist')

## load all conversations
conversations = db.GetRootStatusesHavingReplies(1000000)
for c in conversations: h.loadTree(c)

## save conversationsIds in "set"
import System.Collections.Generic
conversationsIds = System.Collections.Generic.Dictionary[System.Int64, System.Int64]()
def addId(sInfo):
	conversationsIds[sInfo.Status.StatusId] = sInfo.Status.StatusId
	for child in sInfo.Children:
		addId(child)
for status in conversations:
	addId(status)
print 'count of conversationsIds: ' + str(conversationsIds.Values.Count)

# load statuses older than 90days, that is not Timeline(1), Public(3), RequestedConversation(4), Retweet(5)
older = db.GetStatusesFromSql('select * from Status where Source <> 1 and Source <> 3 and Source <> 4 and Source <> 5 and Inserted < ' + 
	str(System.DateTime.Now.AddDays(-30).Ticks))
print "Count of all old statuses: " + str(older.Length)
counter = 0
for o in older:
	if not conversationsIds.ContainsKey(o.Status.StatusId):
		counter = counter + 1
print "Count of statuses to export: " + str(counter)

## export to db
import sys
import StatusDb
sdb = StatusDb.StatusesDbState(exportFile)
counter = 0
for o in older:
	if not conversationsIds.ContainsKey(o.Status.StatusId):
		sys.stdout.write(str(counter) + ' ')
		sdb.SaveStatus(o)
		db.DeleteStatus(o)
		counter = counter+1

	if System.Console.KeyAvailable:
		keyinfo = System.Console.ReadKey()
		if keyinfo.Key == System.ConsoleKey.Escape:
			print "Pressed Esc -> breaking"
			break
print 'done'