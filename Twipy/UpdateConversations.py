import System
from StatusesReplies import *

st = db.GetRootStatusesHavingReplies(1000)
res = []
counter = 1
for status in st:
  #if limits.GetLimits().SearchLimit != None and limits.GetLimits().SearchLimit.Value > System.DateTime.Now:
  if System.Console.KeyAvailable:
    keyinfo = System.Console.ReadKey()
    if keyinfo.Key == System.ConsoleKey.Escape:
      print "Pressed Esc -> breaking"
      break
  if not limits.IsSafeToQueryTwitter():
    print limits.GetLimitsString(), " - ", System.DateTime.Now
    #System.Threading.Thread.Sleep(5000)
    System.Threading.Thread.CurrentThread.Join(1000*60)
    continue
  print "\n-----",counter.ToString()," - ", limits.GetLimitsString(), "\n"
  h.loadTree(status)
  print status.ToString()
  #h.show(status)
  withrepl = findReplies(status)
  res.append(withrepl)
  counter = counter+1
#h.show(res)