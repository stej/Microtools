import clr
clr.AddReference('System.Xml.Linq')

import OAuth
from System import *
from System.Xml.Linq import *

def xname(x): 
  return XName.Get(x)

print 'Getting lists...'
lists = OAuth.requestTwitter("http://api.twitter.com/1/lists/all.xml")

# can not use lists.IsSome or lists.IsNone - exception 'Unhandled Exception: System.InvalidProgramException: Common Language Runtime detected an invalid program.' was thrown
if lists.Value <> None:  
  listsDoc = XDocument.Parse(lists.Value.Item1)

  print "Information about Twitter lists:"

  toprint = []
  for list in listsDoc.Descendants(xname("list")):
    info = (list.Element(xname("id")).Value,
            list.Element(xname("name")).Value,
            list.Element(xname("full_name")).Value,
            list.Element(xname("member_count")).Value,
            list.Element(xname("subscriber_count")).Value)
    toprint.append(info)
    
  if len(toprint) == 0:
   "You have no list"
  else:
   Console.WriteLine("{0,-10}{1,-20}{2,-35}{3,-5}{4,-5}", "id", "name", "full name", "members", "subscribers")
   for (id,n,fn,mc,sc) in toprint:
    Console.WriteLine("{0,-10}{1,-20}{2,-35}{3,-5}{4,-5}", id, n, fn, mc, sc)
else:
  print 'Lists can not be retrieved'