using MonadicSharp.Security.Masking;
using FluentAssertions;
using Xunit;

namespace MonadicSharp.Security.Tests;

public class SecretMaskerTests
{
    [Fact] public void Clean_String_Is_Unchanged() =>
        SecretMasker.Default.Mask("Hello world").Should().Be("Hello world");

    [Fact] public void Jwt_Is_Masked()
    {
        var jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ1c2VyIn0.abc123xyz";
        SecretMasker.Default.Mask($"token={jwt}").Should().Contain("[MASKED]");
    }

    [Fact] public void Registered_Secret_Is_Masked()
    {
        var masker = new SecretMasker().Register("super-secret-key-12345");
        masker.Mask("Using super-secret-key-12345 in config").Should().NotContain("super-secret-key-12345");
    }

    [Fact] public void ContainsSecret_Detects_Known_Value()
    {
        var masker = new SecretMasker().Register("mypassword");
        masker.ContainsSecret("password=mypassword").Should().BeTrue();
    }

    [Fact] public void Null_Input_Returns_Empty() =>
        SecretMasker.Default.Mask(null).Should().Be(string.Empty);
}
