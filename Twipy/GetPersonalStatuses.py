statuses = helper.DownloadAndSavePersonalStatuses()
print 'Count: {0}'.format(statuses.Length)
for status in statuses:
	print status.Item1.UserName