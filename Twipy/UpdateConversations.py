import System
from StatusesReplies import *
st = db.GetRootStatusesHavingReplies(1000)
res = []
counter = 1
for status in st:
  #if limits.GetLimits().SearchLimit != None and limits.GetLimits().SearchLimit.Value > System.DateTime.Now:
  if not limits.IsSafeToQueryTwitter():
    print limits.GetLimitsString()
    System.Threading.Thread.Sleep(1000)
    continue
  print "\n-----",counter.ToString()," - ", limits.GetLimitsString(), "\n"
  h.loadTree(status)
  print status.Text
  #h.show(status)
  if not limits.IsSafeToQueryTwitter():
    print 'limits -> break'
    break
  withrepl = findReplies(status)
  res.append(withrepl)
  counter = counter+1
#h.show(res)