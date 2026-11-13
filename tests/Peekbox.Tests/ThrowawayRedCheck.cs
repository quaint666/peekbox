namespace Peekbox.Tests;

// Throwaway: proves the CI check goes red. This branch is never merged.
public class ThrowawayRedCheck
{
    [Fact]
    public void This_test_fails_on_purpose() => Assert.Fail("deliberate failure to prove the check goes red");
}
