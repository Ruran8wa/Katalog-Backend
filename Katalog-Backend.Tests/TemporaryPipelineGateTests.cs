using NUnit.Framework;

namespace Katalog_Backend.Tests;

[TestFixture]
public sealed class TemporaryPipelineGateTests
{
    [Test]
    public void DeliberatelyFailsToVerifyThePullRequestGate()
    {
        Assert.Fail("Temporary pipeline-gate demonstration test.");
    }
}
