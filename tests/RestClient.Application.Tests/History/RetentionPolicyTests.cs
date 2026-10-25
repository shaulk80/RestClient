using NUnit.Framework;
using RestClient.Application.History;

namespace RestClient.Application.Tests.History;

[TestFixture]
public class RetentionPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly int[] AgesInHours = [1, 2, 3, 4];

    private static readonly int[] OldestTwo = [3, 4];

    // Entries are 1, 2, 3 and 4 hours old. Ages are in whole hours, limits in minutes, so no entry sits on the cutoff.
    [TestCase(10, 150, new[] { 3, 4 })]
    [TestCase(2, 600, new[] { 3, 4 })]
    [TestCase(1, 150, new[] { 2, 3, 4 })]
    [TestCase(10, 600, new int[] { })]
    [TestCase(0, 600, new[] { 1, 2, 3, 4 })]
    public void SelectExpiredRemovesEntriesOutsideTheAgeAndCountLimits(int maxEntries, int maxAgeMinutes, int[] expiredAgesInHours)
    {
        var policy = new RetentionPolicy(maxEntries, TimeSpan.FromMinutes(maxAgeMinutes));
        var entries = AgesInHours.Select(hours => HistoryFixtures.Entry(Now.AddHours(-hours))).ToList();

        var expired = policy.SelectExpired(entries, Now);

        Assert.That(expired.Select(entry => (int)(Now - entry.SentAt).TotalHours), Is.EquivalentTo(expiredAgesInHours));
    }

    [Test]
    public void SelectExpiredGivesTheSameResultForAnyInputOrder()
    {
        var policy = new RetentionPolicy(2, TimeSpan.FromDays(30));
        var entries = AgesInHours.Select(hours => HistoryFixtures.Entry(Now.AddHours(-hours))).ToList();

        var expired = policy.SelectExpired(entries.AsEnumerable().Reverse().ToList(), Now);

        Assert.That(expired.Select(entry => (int)(Now - entry.SentAt).TotalHours), Is.EquivalentTo(OldestTwo));
    }
}
