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
sizes = []
def countSize(sInfo):
  curr = 0
  for ch in sInfo.Children:
    curr = curr + countSize(ch)
  return curr + 1
st = db.GetRootStatusesHavingReplies(100)
for status in st:
  h.loadTree(status)
  sizes.append(countSize(status))

import System
System.Environment.SetEnvironmentVariable( "SHODIR", "C:\\prgs\\dev\Sho 2.0 for .NET 4\\")
import clr
clr.AddReference('System.Windows.Forms')
clr.AddReferenceToFileAndPath('c:\\prgs\\dev\\Sho 2.0 for .NET 4\\bin\\ShoViz.dll')
import ShoNS.Visualization
f = ShoNS.Visualization.ShoPlotHelper.Figure()
f.Bar(sizes)

##############
st = db.GetRootStatusesHavingReplies(100000)
sizes = {}
def countSize(sInfo):
  curr = 0
  for ch in sInfo.Children: curr = curr + countSize(ch)
  return curr + 1
for status in st:
  h.loadTree(status)
  size = countSize(status)
  if not sizes.has_key(size): sizes[size] = 0
  sizes[size] = sizes[size] + 1

import System
System.Environment.SetEnvironmentVariable( "SHODIR", "C:\\prgs\\dev\Sho 2.0 for .NET 4\\")
import clr
clr.AddReference('System.Windows.Forms')
clr.AddReferenceToFileAndPath('c:\\prgs\\dev\\Sho 2.0 for .NET 4\\bin\\ShoViz.dll')
import ShoNS.Visualization
f = ShoNS.Visualization.ShoPlotHelper.Figure()
f.Bar([k for k in sizes.keys()], [sizes[k] for k in sizes.keys()])
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
from ImagesSource import *

a = 0
statuses = h.find("cqrs")
for sInfo in statuses:
 h.show(sInfo)
 ensureStatusImage(sInfo.Status)
 a = a + 1
 if a > 150:  # max 100 users
   break
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
from Microsoft.FSharp.Collections import *
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
          ListModule.OfSeq[Status.statusInfo]([]),
          ListModule.OfSeq[Status.statusInfo](statuses))
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
###################
import StatusDb
sdb = StatusDb.StatusesDbState('d:\\temp\\TwitterConversation\\statuses.export.db')
store = db.GetStatusesFromSql('select * from Status order by Inserted desc limit 0,20')
sdb.SaveStatuses(store)