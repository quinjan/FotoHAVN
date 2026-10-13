using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Tests.Storage;

public sealed class PinHashingTests
{
    [Fact]
    public void A_credential_uses_a_16_byte_salt_a_32_byte_hash_and_at_least_210000_iterations()
    {
        var credential = new Pbkdf2PinHasher().Create("4826");

        Assert.Equal(16, credential.Salt.Length);
        Assert.Equal(32, credential.Hash.Length);
        Assert.True(credential.Iterations >= 210_000);
    }

    [Fact]
    public void The_right_pin_verifies_and_a_wrong_one_does_not()
    {
        var hasher = new Pbkdf2PinHasher();
        var credential = hasher.Create("4826");

        Assert.True(hasher.Verify("4826", credential));
        Assert.False(hasher.Verify("4827", credential));
        Assert.False(hasher.Verify(string.Empty, credential));
    }

    [Fact]
    public void Two_credentials_for_one_pin_differ_by_salt()
    {
        var hasher = new Pbkdf2PinHasher();

        var first = hasher.Create("4826");
        var second = hasher.Create("4826");

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public void A_credential_with_more_iterations_still_verifies()
    {
        var credential = new Pbkdf2PinHasher(Pbkdf2PinHasher.MinimumIterations + 1000).Create("1357");

        Assert.Equal(Pbkdf2PinHasher.MinimumIterations + 1000, credential.Iterations);
        Assert.True(new Pbkdf2PinHasher().Verify("1357", credential));
    }

    [Fact]
    public void A_weak_or_malformed_credential_never_verifies()
    {
        var hasher = new Pbkdf2PinHasher();
        var good = hasher.Create("4826");

        Assert.False(hasher.Verify("4826", good with { Iterations = Pbkdf2PinHasher.MinimumIterations - 1 }));
        Assert.False(hasher.Verify("4826", good with { Hash = good.Hash[..16] }));
        Assert.False(hasher.Verify("4826", good with { Salt = good.Salt[..8] }));
    }

    [Fact]
    public void The_hasher_refuses_too_few_iterations_and_bad_input()
    {
        var hasher = new Pbkdf2PinHasher();

        Assert.Throws<ArgumentOutOfRangeException>(() => new Pbkdf2PinHasher(Pbkdf2PinHasher.MinimumIterations - 1));
        Assert.Throws<ArgumentException>(() => hasher.Create(string.Empty));
        Assert.Throws<ArgumentNullException>(() => hasher.Create(null!));
        Assert.Throws<ArgumentNullException>(() => hasher.Verify(null!, hasher.Create("1")));
        Assert.Throws<ArgumentNullException>(() => hasher.Verify("1", null!));
    }

    [Fact]
    public void The_fast_hasher_verifies_and_refuses_like_the_real_one()
    {
        var hasher = new FastPinHasher();
        var credential = hasher.Create("4826");

        Assert.True(hasher.Verify("4826", credential));
        Assert.False(hasher.Verify("4827", credential));
        Assert.Equal(1, credential.Iterations);
        Assert.Throws<ArgumentException>(() => hasher.Create(string.Empty));
        Assert.Throws<ArgumentNullException>(() => hasher.Verify(null!, credential));
        Assert.Throws<ArgumentNullException>(() => hasher.Verify("1", null!));
    }
}
