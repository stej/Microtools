l = db.GetLastTwitterStatusId()
print "%d" % l
status = db.ReadStatusWithId(l)
print "%d" % status.Value.Children.Count

##############3
l = db.GetLastTwitterStatusId()
print "%d" % l
status = db.ReadStatusWithId(l).Value
helper.loadTree(status)
helper.show(status)
#############
count = 1000
st = db.GetRootStatusesHavingReplies(count)
counter = 0
for status in st:
  counter = counter + 1
  if counter > 950:
    helper.loadTree(status)
helper.show(st)
############
st = db.GetStatusesFromSql("select * from Status where Text like '%ereading.cz%'")
for status in st:
  helper.loadTree(status)
helper.show(st)
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
  helper.loadTree(status)
  sizes.append(countSize(status))

import System
System.Environment.SetEnvironmentVariable( "SHODIR", "C:\\prgs\\dev\Sho 2.0 for .NET 4\\")
import clr
clr.AddReference('System.Windows.Forms')
clr.AddReferenceToFileAndPath('c:\\prgs\\dev\\Sho 2.0 for .NET 4\\bin\\ShoViz.dll')
import ShoNS.Visualization
f = ShoNS.Visualization.ShoPlotHelper.Figure()
f.Bar(sizes)