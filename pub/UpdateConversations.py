import System
from StatusesReplies import *

statuses = db.GetRootStatusesHavingReplies(400)
res = []
i = 0
while i < statuses.Length:
  #if limits.GetLimits().SearchLimit != None and limits.GetLimits().SearchLimit.Value > System.DateTime.Now:
  if System.Console.KeyAvailable:
    keyinfo = System.Console.ReadKey()
    if keyinfo.Key == System.ConsoleKey.Escape:
      print "Pressed Esc -> breaking"
      break
  if not limits.IsSafeToQueryTwitter():
    print '[{0}] {1} - {2}'.format(i, limits.GetLimitsString(), System.DateTime.Now)
    #System.Threading.Thread.Sleep(5000)
    System.Threading.Thread.CurrentThread.Join(1000*60*5)
    continue
  print "\n----- [{0}] - {1}\n".format(i, limits.GetLimitsString())
  status = statuses[i]
  h.loadTree(status)
  print status.ToString()
  #h.show(status)
  withrepl = findReplies(status)
  res.append(withrepl)
  i = i+1
#h.show(res)