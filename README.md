# fgc-notifications-lambda

Substitui o container `fgc-notifications-api` por duas funções **AWS Lambda .NET 8** (AWS SAM), removendo o processo sempre ativo consumindo RabbitMQ via MassTransit:

- **EventProcessor** — acionada diretamente por Event Source Mapping em um broker **Amazon MQ (RabbitMQ gerenciado)**, processa `UserCreatedEvent`/`PaymentProcessedEvent` e grava notificações no DynamoDB.
- **HistoryApi** — `GET /notifications` atrás de API Gateway, autenticada por JWT (mesma chave HS256 de `fgc-users-api`/`fgc-catalog-api`), autorização por role `Admin` feita dentro da própria função.

## Por que Amazon MQ (e não RabbitMQ self-hosted)

O Lambda Event Source Mapping para RabbitMQ só existe contra um broker **Amazon MQ gerenciado** (`docs.aws.amazon.com/lambda/latest/dg/with-mq.html`) — não há suporte a RabbitMQ self-managed como existe para Kafka. Por isso o broker de produção migrou para Amazon MQ (ver `template.yaml`); os serviços .NET publicam nele em produção (`RabbitMq__UseSsl=true`) mas continuam usando RabbitMQ self-hosted no `docker-compose` local.

## Estrutura

```
template.yaml                                  # SAM: Amazon MQ broker, tabela DynamoDB, 2 funções
src/
  Fgc.Notifications.Lambda.Domain/              # Notification, NotificationType, exceções
  Fgc.Notifications.Lambda.Application/         # NotificationService, interfaces, eventos locais,
                                                 # parser do envelope MassTransit
  Fgc.Notifications.Lambda.Infrastructure/       # LoggingEmailSender, DynamoDbNotificationRepository
  Fgc.Notifications.Lambda.EventProcessor/       # Lambda 1 (trigger Amazon MQ)
  Fgc.Notifications.Lambda.HistoryApi/           # Lambda 2 (GET /notifications via API Gateway)
tests/Fgc.Notifications.Lambda.Tests/            # xUnit + Moq
events/                                          # payloads sintéticos (formato real aws:rmq) p/ teste local
```

A lógica de negócio foi portada de `fgc-notifications-api/.../Api/Consumers/*` (a versão que de fato chama `INotificationService` — o container antigo tinha um segundo conjunto de consumers em `Application/Consumers/*` que só logava e nunca foi registrado; **não** é essa a lógica correta).

## Build

```bash
sam build
sam validate --lint
dotnet test tests/Fgc.Notifications.Lambda.Tests
```

## Limitações conhecidas do modelo (não são bugs)

- RabbitMQ via Amazon MQ permite só **1 ambiente de execução concorrente por event source mapping** (processamento sequencial).
- Entrega "at-least-once": reprocessamento pode gerar notificações duplicadas — mesmo risco que já existia no container antigo (MassTransit também não garantia idempotência aqui).
- `PubliclyAccessible: true` no broker evita exigir VPC/subnets (adequado ao escopo acadêmico); produção "de verdade" trocaria isso por acesso privado via VPC.

## Teste local (LocalStack)

A emulação de Amazon MQ no LocalStack é exclusiva do plano Ultimate (pago), e **RabbitMQ não é suportado nem lá** (só ActiveMQ). Ou seja, o event source mapping em si nunca é testável de ponta a ponta localmente — isso é testado por **invocação direta** do `EventProcessorFunction` com um payload sintético no formato real documentado pela AWS (`aws:rmq`, `rmqMessagesByQueue`).

O que **é** testável de ponta a ponta no LocalStack (`docker-compose up -d localstack` em `fgc-orchestration`, ou já de pé junto com o resto do stack):

```bash
export AWS_ACCESS_KEY_ID=local AWS_SECRET_ACCESS_KEY=local AWS_DEFAULT_REGION=us-east-1

# Só os recursos deployáveis no LocalStack Community (o broker Amazon MQ real fica fora)
samlocal build
samlocal deploy --resolve-s3 --stack-name fgc-notifications-lambda --capabilities CAPABILITY_IAM

aws --endpoint-url=http://localhost:4566 dynamodb scan --table-name FgcNotifications
```

`AWS::AmazonMQ::Broker`/`AWS::Lambda::EventSourceMapping` já estão separados em `template-broker.yaml` (deployado só contra a AWS real, depois do stack de `template.yaml`) — `template.yaml` principal é 100% deployável no LocalStack sem eles.

### Invocação direta do EventProcessor (trigger simulado)

```bash
sam local invoke EventProcessorFunction -e events/rabbitmq-user-created.json --env-vars env.local.json
sam local invoke EventProcessorFunction -e events/rabbitmq-payment-approved.json --env-vars env.local.json
sam local invoke EventProcessorFunction -e events/rabbitmq-payment-rejected.json --env-vars env.local.json
```

`env.local.json` (não versionado, exemplo — `8500` é a porta host do `dynamodb-local` no `docker-compose.yml` de `fgc-orchestration`; ajuste se estiver usando um `docker run amazon/dynamodb-local` avulso):
```json
{
  "EventProcessorFunction": {
    "DYNAMODB_LOCAL_ENDPOINT": "http://host.docker.internal:8500"
  },
  "HistoryApiFunction": {
    "DYNAMODB_LOCAL_ENDPOINT": "http://host.docker.internal:8500",
    "JWT_KEY": "FgcUsers-Auth-JWT-Key-2026-Strong-And-Secure",
    "JWT_ISSUER": "Fgc.UsersAPI",
    "JWT_AUDIENCE": "Fgc.CatalogAPI"
  }
}
```

`DYNAMODB_LOCAL_ENDPOINT` precisa estar declarada (mesmo que vazia) em `Environment.Variables` de cada função no `template.yaml` — `sam local invoke --env-vars` só sobrescreve variáveis já declaradas, não injeta variáveis novas. Variáveis com prefixo `AWS_` (`AWS_DYNAMODB_SERVICE_URL`, por exemplo) são reservadas pelo Lambda e descartadas silenciosamente se você tentar defini-las.

Após cada invocação, `aws dynamodb scan --endpoint-url http://localhost:8500 --table-name FgcNotifications` deve mostrar 1 item novo para `welcome`/`payment-approved`, e nenhum para `payment-rejected` (verificado nesta sessão: `welcome` + `payment-approved` geraram exatamente os 2 itens esperados via `sam local invoke` real contra um `dynamodb-local` real).

## Deploy

Contra AWS real, nessa ordem (o segundo template importa outputs exportados pelo primeiro):

```bash
sam deploy --guided --template-file template.yaml --stack-name fgc-notifications-lambda \
  --parameter-overrides JwtKey=<mesma chave HS256 de fgc-users-api>

sam deploy --guided --template-file template-broker.yaml --stack-name fgc-notifications-lambda-broker \
  --parameter-overrides MainStackName=fgc-notifications-lambda
```

A saída `BrokerAmqpEndpoint` (do segundo stack) deve ser propagada para `RabbitMq__Host`/`RabbitMq__Port` (5671) dos `k8s/configmap.yaml` de `fgc-users-api`/`fgc-catalog-api`; a saída `HistoryApiUrl` (do primeiro stack) deve ser propagada para `NOTIFICATIONS_HISTORY_URL` do Kong em `fgc-orchestration`.
