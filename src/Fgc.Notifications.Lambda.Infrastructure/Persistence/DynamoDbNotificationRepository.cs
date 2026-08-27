using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Fgc.Notifications.Lambda.Application.Interfaces;
using Fgc.Notifications.Lambda.Domain.Entities;
using Fgc.Notifications.Lambda.Domain.Enums;

namespace Fgc.Notifications.Lambda.Infrastructure.Persistence;

public class DynamoDbNotificationRepository(IAmazonDynamoDB dynamoDb) : INotificationRepository
{
    private const string TableName = "FgcNotifications";
    // Volume baixo (projeto acadêmico) - partição única é suficiente e mantém a ordenação
    // por data nativa via Query/ScanIndexForward=false no lugar do antigo OrderByDescending(SentAt).
    private const string PartitionKeyValue = "NOTIFICATION";

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["Pk"] = new AttributeValue { S = PartitionKeyValue },
            ["SentAtId"] = new AttributeValue { S = $"{notification.SentAt:O}#{notification.Id}" },
            ["Id"] = new AttributeValue { S = notification.Id.ToString() },
            ["Type"] = new AttributeValue { S = notification.Type.ToString() },
            ["RecipientName"] = new AttributeValue { S = notification.RecipientName },
            ["RecipientEmail"] = new AttributeValue { S = notification.RecipientEmail },
            ["Subject"] = new AttributeValue { S = notification.Subject },
            ["Body"] = new AttributeValue { S = notification.Body },
            ["SentAt"] = new AttributeValue { S = notification.SentAt.ToString("O") }
        };

        await dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = TableName,
            Item = item
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "Pk = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = PartitionKeyValue }
            },
            ScanIndexForward = false
        }, cancellationToken);

        return response.Items.Select(FromItem).ToList();
    }

    private static Notification FromItem(Dictionary<string, AttributeValue> item)
    {
        return Notification.Restore(
            id: Guid.Parse(item["Id"].S),
            type: Enum.Parse<NotificationType>(item["Type"].S),
            recipientName: item["RecipientName"].S,
            recipientEmail: item["RecipientEmail"].S,
            subject: item["Subject"].S,
            body: item["Body"].S,
            sentAt: DateTime.Parse(item["SentAt"].S, null, System.Globalization.DateTimeStyles.RoundtripKind));
    }
}
