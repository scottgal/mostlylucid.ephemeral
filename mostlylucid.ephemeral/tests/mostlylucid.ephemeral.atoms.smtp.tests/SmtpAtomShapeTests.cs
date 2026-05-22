using Xunit;
using Mostlylucid.Ephemeral.Atoms.Smtp;

namespace Mostlylucid.Ephemeral.Atoms.Smtp.Tests;

public class SmtpAtomShapeTests
{
    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SmtpAtom(null!));
    }

    [Fact]
    public void EmailMessage_can_be_constructed()
    {
        var m = new EmailMessage
        {
            To = "test@example.com",
            Subject = "Hello",
            Body = "World"
        };
        Assert.NotNull(m);
        Assert.Equal("test@example.com", m.To);
    }
}
