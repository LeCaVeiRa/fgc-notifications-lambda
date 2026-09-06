# Ponte RabbitMQ -> Lambda sob demanda (ver drain-and-invoke.py). Roda uma vez e sai - não é um
# processo contínuo.
FROM python:3.12-alpine

RUN pip install --no-cache-dir pika boto3

COPY drain-and-invoke.py /usr/local/bin/drain-and-invoke.py
RUN chmod +x /usr/local/bin/drain-and-invoke.py

ENTRYPOINT ["python3", "/usr/local/bin/drain-and-invoke.py"]
