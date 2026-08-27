namespace Fgc.Notifications.Lambda.Application.Events;

// Definido localmente em vez de referenciar o pacote Fgc.MessageContracts: a Lambda não usa
// MassTransit (não há deserialização por tipo CLR via IBus), só desembrulha manualmente o
// envelope JSON publicado pelo MassTransit nos outros serviços. O shape precisa só bater com
// o JSON que chega, não com um tipo CLR específico.
public record UserCreatedEvent(Guid Id, string Name, string Email, DateTime CreatedAt);
