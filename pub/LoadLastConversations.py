a = helper.LoadConversations(10)
for s in a:
 print s.UserName
 helper.LoadChildren(s)
 print s.Children.Count