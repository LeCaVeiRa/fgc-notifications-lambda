#!/bin/sh
set -eu

export AWS_ACCESS_KEY_ID=local
export AWS_SECRET_ACCESS_KEY=local
export AWS_DEFAULT_REGION=us-east-1

samlocal build

samlocal deploy \
    --resolve-s3 \
    --stack-name fgc-notifications-lambda \
    --capabilities CAPABILITY_IAM \
    --no-confirm-changeset \
    --disable-rollback \
    --no-fail-on-empty-changeset \
    --parameter-overrides "JwtKey=${JWT_KEY}"

echo "Deploy concluido. Recursos disponiveis em http://localhost:4566 (tabela FgcNotifications, EventProcessorFunction, HistoryApiFunction)."
