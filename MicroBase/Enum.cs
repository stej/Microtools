namespace MicroBase
{
	public enum FilterType
	{
		UserName,
		Text,
		RTs,
		TimelineStatuses
	}

	public enum StatusSource
	{
		Timeline,				// downloaded as timeline (mentions/friends)
		RequestedConversation,	// requested - either user wants to check conversation where the status is placed or the status is fetched to see where timeline status is rooted
		Search,					// downloaded during search
		Public,					// public statuses
		Retweet
	}
}
