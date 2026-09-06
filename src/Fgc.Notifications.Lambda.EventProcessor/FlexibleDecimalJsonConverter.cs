using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fgc.Notifications.Lambda.EventProcessor;

// MassTransit serializa campos decimal como string no envelope JSON (ex.: "price": "0"), para
// evitar perda de precisão - o parser padrão do System.Text.Json não aceita string onde espera
// number. Sem isso, qualquer PaymentProcessedEvent publicado de verdade (fora dos fixtures
// sintéticos com número puro) falha ao desserializar.
public class FlexibleDecimalJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return decimal.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
        }

        return reader.GetDecimal();
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}
