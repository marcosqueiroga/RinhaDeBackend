using System.Text.Json.Serialization;

namespace RinhaDeBackend
{
    [JsonSerializable(typeof(Transacao))]
    [JsonSerializable(typeof(ResponseExtrato))]
    [JsonSerializable(typeof(ResponseSaldo))]
    [JsonSerializable(typeof(ResponseTransacao))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(DateTime))]
    [JsonSerializable(typeof(DateTime?))]
    [JsonSerializable(typeof(List<Transacao>))]
    internal sealed partial class CustomJsonContext : JsonSerializerContext
    {
    }
}