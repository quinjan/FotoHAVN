using System.Text.Json;

namespace FotoHavn.Protocol;

/// <summary>The wire spelling of protocol enum values (camelCase of the member name), for strings the protocol builds from them.</summary>
public static class ProtocolNames
{
    public static string Of<TEnum>(TEnum value)
        where TEnum : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
