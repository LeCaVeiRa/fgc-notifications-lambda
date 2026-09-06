#!/usr/bin/env python3
"""
Ponte RabbitMQ -> Lambda sob demanda, "serverless-style": roda uma vez quando chamada
(`docker compose run --rm notifications-lambda-bridge`), drena o que estiver parado na fila
`notifications-lambda` (ligada às exchanges fanout que UserCreatedEvent/PaymentProcessedEvent já
usam no RabbitMQ local) e invoca o EventProcessorFunction já deployado no LocalStack para cada
mensagem - exatamente o papel que o Event Source Mapping do Amazon MQ cumpre em produção, que não
existe localmente (RabbitMQ self-hosted não é suportado por Lambda Event Source Mapping, e nem o
LocalStack Community emula Amazon MQ). Não fica escutando - drena o que existe e termina.
"""
import base64
import json
import os
import sys

import boto3
import pika

RABBITMQ_HOST = os.environ.get("RABBITMQ_HOST", "rabbitmq")
RABBITMQ_PORT = int(os.environ.get("RABBITMQ_PORT", "5672"))
RABBITMQ_USER = os.environ.get("RABBITMQ_USER", "admin")
RABBITMQ_PASSWORD = os.environ.get("RABBITMQ_PASSWORD", "admin")
RABBITMQ_VHOST = os.environ.get("RABBITMQ_VHOST", "/")

LOCALSTACK_ENDPOINT = os.environ.get("LOCALSTACK_ENDPOINT", "http://localstack:4566")
AWS_REGION = os.environ.get("AWS_REGION", "us-east-1")

QUEUE_NAME = "notifications-lambda"
EXCHANGES = [
    "Fgc.MessageContracts.Events:UserCreatedEvent",
    "Fgc.MessageContracts.Events:PaymentProcessedEvent",
]


def connect_rabbitmq():
    credentials = pika.PlainCredentials(RABBITMQ_USER, RABBITMQ_PASSWORD)
    params = pika.ConnectionParameters(
        host=RABBITMQ_HOST,
        port=RABBITMQ_PORT,
        virtual_host=RABBITMQ_VHOST,
        credentials=credentials,
    )
    connection = pika.BlockingConnection(params)
    channel = connection.channel()

    channel.queue_declare(queue=QUEUE_NAME, durable=True)
    for exchange in EXCHANGES:
        # Declara a exchange (idempotente - mesmo tipo/durabilidade que o MassTransit já usa) para
        # o bind funcionar mesmo que a ponte rode antes de qualquer publish real ter acontecido.
        channel.exchange_declare(exchange=exchange, exchange_type="fanout", durable=True)
        channel.queue_bind(queue=QUEUE_NAME, exchange=exchange)

    return connection, channel


def find_event_processor_function_name(lambda_client):
    paginator = lambda_client.get_paginator("list_functions")
    for page in paginator.paginate():
        for fn in page["Functions"]:
            if "EventProcessorFunction" in fn["FunctionName"]:
                return fn["FunctionName"]
    return None


def build_rmq_event(properties, redelivered, body):
    return {
        "eventSource": "aws:rmq",
        "eventSourceArn": "arn:aws:mq:us-east-1:000000000000:broker:fgc-notifications-bridge:b-local",
        "rmqMessagesByQueue": {
            f"{QUEUE_NAME}::/": [
                {
                    "basicProperties": {
                        "contentType": properties.content_type,
                        "contentEncoding": properties.content_encoding,
                        "headers": properties.headers or {},
                        "deliveryMode": properties.delivery_mode,
                        "priority": properties.priority,
                        "correlationId": properties.correlation_id,
                        "replyTo": properties.reply_to,
                        "expiration": properties.expiration,
                        "messageId": properties.message_id,
                        "timestamp": properties.timestamp,
                        "type": properties.type,
                        "userId": properties.user_id,
                        "appId": properties.app_id,
                        "clusterId": properties.cluster_id,
                        "bodySize": len(body),
                    },
                    "redelivered": redelivered,
                    "data": base64.b64encode(body).decode("ascii"),
                }
            ]
        },
    }


def main():
    connection, channel = connect_rabbitmq()

    lambda_client = boto3.client(
        "lambda",
        endpoint_url=LOCALSTACK_ENDPOINT,
        region_name=AWS_REGION,
        aws_access_key_id="local",
        aws_secret_access_key="local",
    )
    function_name = find_event_processor_function_name(lambda_client)
    if function_name is None:
        print(
            "Nenhuma EventProcessorFunction encontrada no LocalStack - rode "
            "`docker compose run --rm notifications-lambda-deploy` primeiro.",
            file=sys.stderr,
        )
        connection.close()
        sys.exit(1)

    print(f"Drenando fila '{QUEUE_NAME}' e invocando '{function_name}'...")

    processed = 0
    failed = 0
    while True:
        method_frame, header_frame, body = channel.basic_get(queue=QUEUE_NAME, auto_ack=False)
        if method_frame is None:
            break

        payload = build_rmq_event(header_frame, method_frame.redelivered, body)
        try:
            response = lambda_client.invoke(
                FunctionName=function_name,
                Payload=json.dumps(payload).encode("utf-8"),
            )
            if response.get("FunctionError"):
                error_body = response["Payload"].read().decode("utf-8", errors="replace")
                print(f"  [ERRO] invocação retornou FunctionError: {error_body}", file=sys.stderr)
                channel.basic_nack(delivery_tag=method_frame.delivery_tag, requeue=False)
                failed += 1
            else:
                channel.basic_ack(delivery_tag=method_frame.delivery_tag)
                processed += 1
        except Exception as exc:  # noqa: BLE001 - ferramenta de dev local, log e segue
            print(f"  [ERRO] falha ao invocar a Lambda: {exc}", file=sys.stderr)
            channel.basic_nack(delivery_tag=method_frame.delivery_tag, requeue=False)
            failed += 1

    connection.close()
    print(f"Concluído: {processed} mensagem(ns) entregues, {failed} falharam.")


if __name__ == "__main__":
    main()
