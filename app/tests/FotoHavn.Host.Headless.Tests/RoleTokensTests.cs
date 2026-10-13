namespace FotoHavn.Host.Headless.Tests;

public sealed class RoleTokensTests
{
    [Fact]
    public void Tokens_are_256_bit_lower_case_hex_distinct_per_role_and_per_start()
    {
        var first = RoleTokens.Create();
        var second = RoleTokens.Create();

        foreach (var token in new[] { first.Operator, first.Guest })
        {
            Assert.Matches("^[0-9a-f]{64}$", token);
        }

        Assert.NotEqual(first.Operator, first.Guest);
        Assert.NotEqual(first.Operator, second.Operator);
        Assert.NotEqual(first.Guest, second.Guest);
    }

    [Fact]
    public void Each_token_maps_to_its_role()
    {
        var tokens = new RoleTokens("operator-token", "guest-token");

        Assert.True(tokens.TryGetRole("operator-token", out var operatorRole));
        Assert.Equal(Role.Operator, operatorRole);
        Assert.True(tokens.TryGetRole("guest-token", out var guestRole));
        Assert.Equal(Role.Guest, guestRole);
        Assert.Equal("operator-token", tokens.For(Role.Operator));
        Assert.Equal("guest-token", tokens.For(Role.Guest));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("OPERATOR-TOKEN")]
    [InlineData("operator-toke")]
    [InlineData("operator-tokenx")]
    [InlineData(" guest-token")]
    public void Anything_else_maps_to_no_role(string? token)
    {
        Assert.False(new RoleTokens("operator-token", "guest-token").TryGetRole(token, out _));
    }

    [Fact]
    public void Tokens_must_be_present_and_different()
    {
        Assert.Throws<ArgumentException>(() => new RoleTokens("same", "same"));
        Assert.Throws<ArgumentException>(() => new RoleTokens("", "guest"));
        Assert.Throws<ArgumentNullException>(() => new RoleTokens("operator", null!));
    }
}
