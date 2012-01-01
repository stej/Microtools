############### 
## common
def countSize(sInfo):
  curr = 0
  for ch in sInfo.Children: curr = curr + countSize(ch)
  return curr + 1
def getShoFigure():
  import System
  System.Environment.SetEnvironmentVariable( "SHODIR", "C:\\prgs\\dev\Sho 2.0 for .NET 4\\")
  import clr
  clr.AddReference('System.Windows.Forms')
  clr.AddReferenceToFileAndPath('c:\\prgs\\dev\\Sho 2.0 for .NET 4\\bin\\ShoViz.dll')
  import ShoNS.Visualization
  return ShoNS.Visualization.ShoPlotHelper.Figure()
def statusInfoListToFSharpList(l):
  from Microsoft.FSharp.Collections import *
  import System.Collections.Generic
  from StatusesReplies import *
  import Status
  return ListModule.OfSeq[Status.statusInfo](l)
def getMonthFromStatus(status):
  from System import DateTime
  date = status.Status.Date
  return DateTime(date.Year, date.Month, 1)
def getWeekFromStatus(status):
  from System import DateTime
  date = status.Status.Date
  return DateTime(date.Year, date.Month, date.Day).AddDays(- int(date.DayOfWeek))
def getTimelineByTimes(statuses, dateFromStatusGetter, nextDateGetter):
  sortedStatuses = sorted(statuses, key = lambda status: status.Status.Date)
  if sortedStatuses.count == 0: return
  if sortedStatuses.count == 1: yield [dateFromStatusGetter(sortedStatuses[0])]; return
  first, last = dateFromStatusGetter(sortedStatuses[0]), dateFromStatusGetter(sortedStatuses[-1])
  while first <= last:
    yield first
    first = nextDateGetter(first)
def getTimelineByMonths(statuses):
  return [t for t in getTimelineByTimes(statuses, getMonthFromStatus, lambda date: date.AddMonths(1))]
def getTimelineByWeeks(statuses):
  return [t for t in getTimelineByTimes(statuses, getWeekFromStatus, lambda date: date.AddDays(7))]
def getDictBy(statuses, keyFunc):
  ret = {}
  for status in statuses:
    key = keyFunc(status)
    if ret.has_key(key):
      s = ret[key] + [status]
      ret[key] = s
    else:
      ret[key] = [status]
  return ret
def getEmbeddedSho():
  import clr
  clr.AddReference('System.Windows.Forms')
  clr.AddReferenceToFileAndPath('c:\\prgs\\dev\\Sho 2.0 for .NET 4\\bin\\EmbeddedSho.dll')
  from ShoNS.Hosting import *

  es = EmbeddedSho('c:\\prgs\\dev\\Sho 2.0 for .NET 4')
  return es

#############
l = db.GetLastTimelineId()
status = db.ReadStatusWithId(l).Value
h.loadTree(status)
h.show(status)

#############
count = 1000
st = db.GetRootStatusesHavingReplies(count)
counter = 0
for status in st:
  counter = counter + 1
  if counter > 950:
    h.loadTree(status)
h.show(st)

############
h.show(h.find("cqrs"))

#############3
size = 0 
def countSize(status):
  global size
  size = size + 1
  for ch in status.Children:
    countSize(ch)
for s in st:
  countSize(s)
print size 

##############
#graf s velikostmi 100 tweetu
#import common: countSize, getShoFigure
sizes = []
st = db.GetRootStatusesHavingReplies(100)
for status in st:
  h.loadTree(status)
  sizes.append(countSize(status))

getShoFigure().Bar(sizes)

##############
# graf, x = velikost konverzace, y = pocet konverzaci dane velikosti
#import common: countSize, getShoFigure
st = db.GetRootStatusesHavingReplies(100000)
sizes = {}
for status in st:
  h.loadTree(status)
  size = countSize(status)
  if not sizes.has_key(size): sizes[size] = 0
  sizes[size] = sizes[size] + 1

getShoFigure().Bar([k for k in sizes.keys()], [sizes[k] for k in sizes.keys()])
##############
# graf s uzivateli, kteri maji nejvetsi konverzace
#import common: countSize, getShoFigure
st = db.GetRootStatusesHavingReplies(1000000)
sizes = {}
for status in st:
  userName = status.Status.UserName
  h.loadTree(status)
  size = countSize(status)
  if not sizes.has_key(userName): sizes[userName] = 0
  sizes[userName] = max(size, sizes[userName])

treshold = 50
getShoFigure().Bar([k for k in sizes.keys() if sizes[k] > treshold], [sizes[k] for k in sizes.keys() if sizes[k] > treshold])

##############
# graf s uzivateli, na ktere reagoval nejvetsi pocet ostatnich (jen 1st level odpoved)
#import common: countSize, getShoFigure
st = db.GetRootStatusesHavingReplies(1000000)
sizes = {}
for status in st:
  userName = status.Status.UserName
  h.loadTree(status)
  replies = status.Children.Count
  if not sizes.has_key(userName): sizes[userName] = 0
  sizes[userName] = max(replies, sizes[userName])

treshold = 20
keys = sorted([k for k in sizes.keys() if sizes[k] > treshold], key = lambda k: sizes[k])
getShoFigure().Bar(keys, [sizes[k] for k in keys])

##############
# graf porovnavajici favourites pro stejcz, kolman, borekb a rarous
#import common: getEmbeddedSho, getTimelineByMonths, getMonthFromStatus, getDictBy
stejcz = db.GetStatusesFromSql("select * from Status where UserName = 'stejcz'")
kolman = db.GetStatusesFromSql("select * from Status where UserName = 'kolman'")
borekb = db.GetStatusesFromSql("select * from Status where UserName = 'borekb'")
alesroubicek = db.GetStatusesFromSql("select * from Status where UserName = 'alesroubicek'")
stejczByMonth = getDictBy(stejcz, getMonthFromStatus)
kolmanByMonth = getDictBy(kolman, getMonthFromStatus)
borekbByMonth = getDictBy(borekb, getMonthFromStatus)
roubicekByMonth = getDictBy(alesroubicek, getMonthFromStatus)
months = [m for m in getTimelineByMonths(stejcz)]
def getFavsDiff(statuses, month):
  prev = month.AddMonths(-1)
  if statuses.has_key(month) and statuses.has_key(prev):
    return statuses[month][-1].Status.UserFavoritesCount - statuses[prev][-1].Status.UserFavoritesCount
  else: return 0
es = getEmbeddedSho()
es.SetPythonVariable("months", months);
es.SetPythonVariable("stejcz", [getFavsDiff(stejczByMonth, m) for m in months]);
es.SetPythonVariable("kolman", [getFavsDiff(kolmanByMonth, m) for m in months]);
es.SetPythonVariable("borekb", [getFavsDiff(borekbByMonth, m) for m in months]);
es.SetPythonVariable("roubicek", [getFavsDiff(roubicekByMonth, m) for m in months]);
es.ExecutePython("bar(months, stejcz, 'b', months, kolman, 'g', months, borekb, 'r', months, roubicek, 'y')");
es.ExecutePython("legend('stejcz', 'kolman', 'borekb', 'roubicek')")
# druha moznost:
#fig = getShoFigure()
#fig.Hold = True
#fig.Plot(months, [getFavsCount(stejczByMonth, m) for m in months], ".-")
#fig.Plot(months, [getFavsCount(kolmanByMonth, m) for m in months], "..")
#fig.Plot(months, [getFavsCount(borekbByMonth, m) for m in months], "..--")
#fig.Plot(months, [getFavsCount(roubicekByMonth, m) for m in months], "-")

##############
# graf s pro stejcz, kde se zobrazi pocet timeline tweetu k precteni po tydnech
#import common: getShoFigure, getTimelineByMonths, getMonthFromStatus, getDictBy
from System import DateTime
st = db.GetStatusesFromSql("select * from Status where Source = 1 or Source = 5")  # timeline or retweet
statusesByWeek = getDictBy(st, getWeekFromStatus)
weeks = [w for w in getTimelineByWeeks(st) if w > DateTime(2010, 10, 1)]
def getStatusesCount(week):
  if statusesByWeek.has_key(week):  return statusesByWeek[week].Count
  else: return 0
getShoFigure().Bar(weeks, [getStatusesCount(w) for w in weeks])

##############
# export statusu s nejvetsimi konverzacemi
#import common: countSize, statusInfoListToFSharpList
st = db.GetRootStatusesHavingReplies(1000000)
sizes = {}
for status in st:
  userName = status.Status.UserName
  h.loadTree(status)
  size = countSize(status)
  if not sizes.has_key(userName): sizes[userName] = (0, None)
  if size > sizes[userName][0]:
    sizes[userName] = (size, status)

treshold = 50
aboveTreshold = [k for k in sizes.keys() if sizes[k][0] > treshold]
sortedKeys = sorted(aboveTreshold, key=lambda k: sizes[k][0])
toExport = statusInfoListToFSharpList([sizes[key][1] for key in sortedKeys])
h.exportToHtml(toExport)
###################
st = db.GetStatusesFromSql("select * from Status where UserName like 'AugiCZ' and ReplyTo=-1")
for status in st:
  h.loadTree(status)
h.exportToHtml(st)
###################
st = db.GetRootStatusesHavingReplies(10)
for sInfo in st:
    h.loadTree(sInfo)
    print sInfo.Status.Text
h.exportToHtml(st)
###################
# ensure images for users from given query
c
###################
print limits.GetLimitsString()
statuses = h.DownloadAndSavePersonalStatuses()
print limits.GetLimitsString()
h.show(statuses)
###################
# get status somehow (e.g. load from db)
status = db.ReadStatusWithId(91945905766408192)
h.show(status)
from StatusesReplies import *
withrepl = findReplies(status.Value)
h.show(withrepl)
####################
# take last 300 statuses and try to root them - find conversations
# import common: statusInfoListToFSharpList
import System.Collections.Generic
from StatusesReplies import *
import Status

sql = """
    select s.* from Status s 
    where exists (select StatusId from Status s0 where s0.ReplyTo = s.StatusId)
    order by s.StatusId desc 
    limit 0, 10"""
statuses = db.GetStatusesFromSql(sql)
rooted = h.RootConversationsWithNoDownload(
          statusInfoListToFSharpList([]),
          statusInfoListToFSharpList(statuses))
toshow = System.Collections.Generic.List[Status.statusInfo]()
for sInfo in rooted:
    if sInfo.Children.Count > 0:
        toshow.Add(sInfo)
h.show(toshow)
###################
# register on Twitter
import OAuth
limits.Stop()
OAuth.registerOnTwitter()
###################################################
import System.IO
exportFile = 'd:\\temp\\TwitterConversation\\bin\\Debug\\statuses.export.db'
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
# (values are: | Timeline -> 1, Search -> 2, Public -> 3, RequestedConversation -> 4, Retweet -> 5, Undefined -> 1000)
older = db.GetStatusesFromSql('select * from Status where Source <> 1 and Source <> 3 and Source <> 4 and Source <> 5 and Inserted < ' + 
	str(System.DateTime.Now.AddDays(-30).Ticks))
print "Count of all old statuses: " + str(older.Length)
counter = 0
for o in older:
	if not conversationsIds.ContainsKey(o.Status.StatusId):
		counter = counter + 1
print "Count of statuses to export: " + str(counter)

## export to external db
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

#########################
## import from other db
from System import *
import System.IO
import StatusDb

fromDate = DateTime(2011, 11, 28, 18, 0, 0)   #yyyy MM dd HH mm ss

fromFile = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, 'statuses.from.db')
if not System.IO.File.Exists(fromFile):
	raise Exception('File ' + fromFile + ' does not exist')

toFile = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, 'statuses.to.db')
if not System.IO.File.Exists(toFile):
	raise Exception('File ' + toFile + ' does not exist')
	
fromdb = StatusDb.StatusesDbState(fromFile)
todb = StatusDb.StatusesDbState(toFile)

statuses = db.GetStatusesFromSql("select * from Status where Inserted > " + fromDate.Ticks.ToString())
for s in statuses:
	stored = todb.ReadStatusWithId(s.Status.StatusId)
	if stored == None:
		print ('Storing: ' + s.ToString())
		todb.SaveStatus(s)
	else:
		print ('Stored: ' + stored.Value.ToString())
########################
# sorts long urls by counts of domains
u = urls.GetUrlsFromSql('select * from UrlTranslation')
from System.Text.RegularExpressions import *
counts = {}
for url in u:
  link = Regex.Replace(url.LongUrl, '^(https?://[^/]+).*', '$1')
  if not counts.has_key(link): counts[link] = 0
  counts[link] = counts[link] + 1

keys = sorted(counts.keys(), key = lambda k: counts[k])
for k in keys:
  print '{0} - {1}'.format(counts[k], k)