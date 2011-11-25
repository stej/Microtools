import System
from StatusesReplies import *

statuses = db.GetRootStatusesHavingReplies(400)
res = []
i = 0
personalStatusesDownloaded = False
while i < statuses.Length:
  #if limits.GetLimits().SearchLimit != None and limits.GetLimits().SearchLimit.Value > System.DateTime.Now:
  if System.Console.KeyAvailable:
    keyinfo = System.Console.ReadKey()
    if keyinfo.Key == System.ConsoleKey.Escape:
      print "Pressed Esc -> breaking"
      break
      
  if not limits.IsSafeToQueryTwitter():
    # lets try to download perosnal statuses.. assuming that 2 requests are ok :)
    if not personalStatusesDownloaded:
      statuses = h.DownloadAndSavePersonalStatuses()
      print 'Personal statuses count: {0}'.format(statuses.Length)
      for status in statuses:
        print '{0}'.format(status.ToString())
      personalStatusesDownloaded = True

    print '[{0}] {1} - {2}'.format(i, limits.GetLimitsString(), System.DateTime.Now)
    System.Threading.Thread.CurrentThread.Join(1000*60*10)
    continue
  print "\n----- [{0}] - {1}\n".format(i, limits.GetLimitsString())
  status = statuses[i]
  h.loadTree(status)
  print status.ToString()
  
  withrepl = findReplies(status)
  res.append(withrepl)
  i = i+1
  personalStatusesDownloaded = False