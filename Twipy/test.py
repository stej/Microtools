l = db.GetLastTwitterStatusId()
print "%d" % l
status = db.ReadStatusWithId(l)
print "%d" % status.Value.Children.Count
##############3
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
for s in statuses:
  countSize(s)
print size

##############
sizes = []
def countSize(status):
  curr = 0
  for ch in status.Children:
    curr = curr + countSize(ch)
  return curr + 1
st = db.GetRootStatusesHavingReplies(500)
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
def countSize(status):
  curr = 0
  for ch in status.Children: curr = curr + countSize(ch)
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

####################
# I have statuses in st, then display some of them:
import Microsoft.FSharp.Collections
list = Microsoft.FSharp.Collections.ListModule.OfSeq[Status+status]([st[0]])
a = Microsoft.FSharp.Collections.FSharpList[Status+status](10, list)
print a.GetType()
####################
st = db.GetRootStatusesHavingReplies(2)
for status in st:
  h.loadTree(status)
h.exportToHtml(st)
###################
st = db.GetStatusesFromSql("select * from Status where UserName like 'AugiCZ' and ReplyTo=-1")
for status in st:
  h.loadTree(status)
h.exportToHtml(st)
###################
st = db.GetRootStatusesHavingReplies(100000)
for status in st:
    h.loadTree(status)
    print status.Text
h.exportToHtml(st)
###################
# ensure images for users from given query
from ImagesSource import *

a = 0
statuses = h.find("cqrs")
for status in statuses:
 h.show(status)
 ensureStatusImage(status)
 a = a + 1
 if a > 100:  # max 100 users
   break
###################
print limits.GetLimitsString()
statuses = h.DownloadAndSavePersonalStatuses()
print limits.GetLimitsString()
h.show(statuses)
###################
# get status somehow (e.g. load from db)
status = ......
h.show(status)
from StatusesReplies import *
withrepl = findReplies(status)
h.show(withrepl)
###################
from StatusesReplies import *
st = db.GetRootStatusesHavingReplies(1000)
res = []
counter = 1
for status in st:
  print "\n-----",counter.ToString(),"\n", limits.GetLimitsString(), "\n"
  h.loadTree(status)
  print status.Text
  h.show(status)
  if not limits.IsSafeToQueryTwitter():
    print 'limits -> break'
    break
  withrepl = findReplies(status)
  res.append(withrepl)
  counter = counter+1
h.show(res)