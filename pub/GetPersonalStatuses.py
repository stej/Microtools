statuses = h.DownloadAndSavePersonalStatuses()
print 'Count: {0}'.format(statuses.Length)
for status in statuses:
	print '{0}'.format(status.ToString())