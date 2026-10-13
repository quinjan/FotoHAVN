using System.Text.Json;

namespace FotoHavn.Protocol.Tests;

public sealed class ProtocolJsonTests
{
    [Fact]
    public void Options_use_camel_case_property_names()
    {
        var json = JsonSerializer.Serialize(new { CommandType = "ready" }, ProtocolJson.Options);

        Assert.Equal("""{"commandType":"ready"}""", json);
    }
}
