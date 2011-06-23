statuses = helper.DownloadAndSavePersonalStatuses()
print 'Count: {0}'.format(statuses.Length)
for status in statuses:
	print '{0} - {1}'.format(status.Item1.UserName, status.Item1.Text)