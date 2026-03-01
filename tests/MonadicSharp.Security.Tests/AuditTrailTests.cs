using MonadicSharp.Security.Audit;
using MonadicSharp.Security.Errors;
using FluentAssertions;
using Xunit;

namespace MonadicSharp.Security.Tests;

public class AuditTrailTests
{
    [Fact] public void Record_Adds_Event()
    {
        var trail = new AuditTrail("session-1");
        trail.Record("Agent1", "Action", "desc", true);
        trail.Events.Should().HaveCount(1);
    }

    [Fact] public void Events_Are_Immutable_After_Record()
    {
        var trail = new AuditTrail();
        trail.Record("A", "T", "d", true);
        var snapshot = trail.Events;
        trail.Record("B", "T", "d", true);
        snapshot.Should().HaveCount(1); // snapshot unchanged
        trail.Events.Should().HaveCount(2);
    }

    [Fact] public void Security_Violation_Sets_HasSecurityViolations()
    {
        var trail = new AuditTrail();
        var err = SecurityError.PromptInjectionDetected("TestRule");
        trail.RecordSecurityViolation("Agent", "Injection", err);
        trail.HasSecurityViolations.Should().BeTrue();
    }

    [Fact] public void Secrets_Are_Masked_In_Descriptions()
    {
        var masker = new MonadicSharp.Security.Masking.SecretMasker().Register("topsecret123");
        var trail = new AuditTrail(masker: masker);
        trail.Record("A", "T", "Used topsecret123 as key", true);
        trail.Events[0].Description.Should().NotContain("topsecret123");
    }

    [Fact] public void ForAgent_Filters_Correctly()
    {
        var trail = new AuditTrail();
        trail.Record("Alpha", "T", "d", true);
        trail.Record("Beta", "T", "d", true);
        trail.ForAgent("Alpha").Should().HaveCount(1);
    }
}
