using DinamicaBackupAgent;

public sealed class WindowsTaskIdentityTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("  ", "")]
    [InlineData("user", @"PC01\user")]
    [InlineData(@" .\user ", @"PC01\user")]
    [InlineData(@"DOMAIN\user", @"DOMAIN\user")]
    [InlineData("user@example.com", "user@example.com")]
    public void NormalizePreservesExplicitIdentityAndExpandsLocalNames(string input, string expected)
        => Assert.Equal(expected, WindowsTaskIdentity.Normalize(input, "PC01"));
}
