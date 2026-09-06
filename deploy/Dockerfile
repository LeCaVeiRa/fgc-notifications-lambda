# Roda `samlocal build && samlocal deploy` automaticamente contra o LocalStack, substituindo o
# passo manual documentado em README.md ("Teste local (LocalStack)"). Só existe para isso - não é
# a imagem de execução do Lambda em si.
FROM mcr.microsoft.com/dotnet/sdk:8.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-pip zip \
    && rm -rf /var/lib/apt/lists/* \
    && pip3 install --break-system-packages aws-sam-cli aws-sam-cli-local

# `sam build` (runtime dotnet8) instala e invoca a ferramenta global `dotnet-lambda`, que o dotnet
# CLI publica em ~/.dotnet/tools - fora do PATH por padrao nesta imagem.
ENV PATH="${PATH}:/root/.dotnet/tools"

WORKDIR /app
COPY template.yaml ./
COPY src/ ./src/

COPY deploy/deploy.sh /usr/local/bin/deploy.sh
RUN chmod +x /usr/local/bin/deploy.sh

ENTRYPOINT ["/usr/local/bin/deploy.sh"]
