First run register.bat and allow the applications to access your Twitter account (only read-only access).
New file twitter.accesstoken.txt should appear in the directory.

And then you may try to run 
TwitterClient - downloads timeline, mentions and retweets 
TwitterConversation
	- if no command line argument is specified, it just shows all last conversations stored in database
	- if command line arg is specified, (e.g. TwitterConversation 91137008386318336), it shows the conversation if already
		stored in db or only downloads the tweet
		Then you may click Update to download the replies