using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fgc.Notifications.Lambda.Application.Messaging;

// A Lambda não usa MassTransit (não há IBus consumindo do RabbitMQ/Amazon MQ) - o corpo AMQP
// que chega via Event Source Mapping é o envelope JSON que o MassTransit grava do lado do
// publisher. Precisamos desembrulhar isso manualmente: "messageType" é um array de URNs
// (ex.: "urn:message:Fgc.MessageContracts.Events:UserCreatedEvent"), o tipo real do evento é
// o sufixo depois do último ':'.
public class MassTransitEnvelope
{
    [JsonPropertyName("messageType")]
    public string[] MessageType { get; set; } = [];

    [JsonPropertyName("message")]
    public JsonElement Message { get; set; }

    public string? ResolveEventTypeName()
    {
        var urn = MessageType.FirstOrDefault();
        if (urn is null)
        {
            return null;
        }

        var lastColon = urn.LastIndexOf(':');
        return lastColon >= 0 ? urn[(lastColon + 1)..] : urn;
    }

    public static MassTransitEnvelope? Parse(string json)
    {
        return JsonSerializer.Deserialize<MassTransitEnvelope>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}
