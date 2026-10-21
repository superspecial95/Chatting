using ChatServer.Services;
using Xunit;

namespace ChatServer.Tests;

public class OnlinePresenceTrackerTests
{
    [Fact]
    public void Tracker_only_marks_a_user_offline_after_the_last_connection_closes()
    {
        var tracker = new OnlinePresenceTracker();

        Assert.True(tracker.AddConnection(42));
        Assert.False(tracker.AddConnection(42));
        Assert.False(tracker.RemoveConnection(42));
        Assert.True(tracker.RemoveConnection(42));
        Assert.False(tracker.RemoveConnection(42));
    }
}
