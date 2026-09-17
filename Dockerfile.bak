# ─── Stage 1: сборка ────────────────────────────────────────────────────

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

WORKDIR /app



ARG HTTP_PROXY

ARG HTTPS_PROXY

ARG NO_PROXY

ENV HTTP_PROXY=$HTTP_PROXY

ENV HTTPS_PROXY=$HTTPS_PROXY

ENV NO_PROXY=$NO_PROXY



# Доверяем корпоративному прокси-сертификату

COPY corp-ca.crt /usr/local/share/ca-certificates/corp-ca.crt
COPY domain-ca.crt /usr/local/share/ca-certificates/domain-ca.crt

RUN update-ca-certificates



# Чистый NuGet.config без Windows-путей (fallback package folder),

# которых нет и не может быть в Linux-контейнере

COPY nuget.docker.config ./NuGet.Config

COPY *.csproj ./

RUN dotnet restore

COPY . .

RUN dotnet publish -c Release -o /app/publish --no-restore



# ─── Stage 2: финальный образ ───────────────────────────────────────────

FROM mcr.microsoft.com/dotnet/aspnet:10.0

# Служба каталогов работает через нативную OpenLDAP: без неё
# System.DirectoryServices.Protocols падает на инициализации типа.
ARG HTTP_PROXY
ARG HTTPS_PROXY
# Корневой сертификат домена — без доверия к нему шифрованное соединение
# со службой каталогов не устанавливается.
COPY domain-ca.crt /usr/local/share/ca-certificates/domain-ca.crt
RUN http_proxy=$HTTP_PROXY https_proxy=$HTTPS_PROXY apt-get update && \
    http_proxy=$HTTP_PROXY https_proxy=$HTTPS_PROXY apt-get install -y --no-install-recommends libldap2 libsasl2-2 ca-certificates && \
    update-ca-certificates && \
    apt-get clean

WORKDIR /app

# Клиент OpenLDAP не берёт доверенные из системы сам: без явного пути
# шифрованное соединение с каталогом не устанавливается.
RUN mkdir -p /etc/ldap && printf 'TLS_CACERT /etc/ssl/certs/ca-certificates.crt\nTLS_REQCERT demand\n' > /etc/ldap/ldap.conf

COPY --from=builder /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "delosfera-server.dll"]