# Онлайн-соредактирование вложений (COOP-1, OnlyOffice)

Совместное редактирование, рецензирование и комментарии для вложений `docx/xlsx/pptx`
через **OnlyOffice Document Server** (внешняя инфраструктура).

## Компоненты

- **Backend** — модуль `Modules/Coauthoring`:
  - `GET /api/coauthoring/config/{attachmentId}` — конфиг редактора (подписан JWT).
  - `GET /api/coauthoring/download/{attachmentId}?token=…` — отдаёт файл Document Server'у по временной подписанной ссылке.
  - `POST /api/coauthoring/callback?attachmentId=…` — приём результата правок (сохранение новой версии).
- **Хранилище версий** — таблица `attachment_version` (история объектов в MinIO; прежние версии не перезатираются).
- **Frontend** — обёртка редактора (`components/editor/OnlyOfficeEditor.tsx`) + кнопка «Открыть для совместного редактирования» в панели вложений.

## Поток

1. Клиент нажимает «Открыть для совместного редактирования» → `GET /config/{attachmentId}`.
2. Backend проверяет доступ к файлу (`IFileAccessAuthorizer`), определяет режим
   (`edit` для автора карточки / администратора, иначе `review`+комментарии),
   формирует `document.url` (подписанная ссылка на скачивание из MinIO через наш API,
   `key` = версия+хеш), `callbackUrl` и подписывает весь конфиг JWT (`OnlyOffice:JwtSecret`).
3. Клиент грузит `api.js` c `ServerUrl` и открывает редактор.
4. Document Server забирает файл по `document.url`, пользователи правят/комментируют.
5. По завершении DS шлёт `callback` (`status=2`/`6`); backend валидирует JWT тела,
   скачивает отредактированный файл, кладёт **новой версией** в MinIO, обновляет
   `FileAttachment` на актуальную версию, аннулирует подписи под вложением (SIG-01)
   и пишет запись в аудит (`CoauthoringSession / VersionSaved`).

## Необходимая инфраструктура

1. Поднять Document Server (отдельный compose, текущий не трогает):
   ```bash
   export ONLYOFFICE_JWT_SECRET="$(openssl rand -base64 48)"
   docker compose -f docker-compose.onlyoffice.yml up -d
   ```
2. Настроить приложение (env / `dotnet user-secrets`) — секреты НЕ в `appsettings.json`:
   ```
   OnlyOffice__ServerUrl    = http://localhost:8080
   OnlyOffice__JwtSecret    = <тот же ONLYOFFICE_JWT_SECRET>
   OnlyOffice__ApiPublicUrl = http://host.docker.internal:5199   # адрес нашего API, видимый из контейнера DS
   ```
3. Frontend: адрес DS клиент берёт из ответа `/config` (`serverUrl`), отдельной
   настройки не требуется.

Без заданных `ServerUrl`/`JwtSecret` интеграция выключена: `/config` вернёт 400, кнопка
в UI работать не будет — остальная система не затрагивается.

## Безопасность

- Конфиг редактора и тело callback'а подписаны HMAC-SHA256 общим секретом
  (`OnlyOffice:JwtSecret`); callback без действительной подписи отклоняется.
- Ссылка на скачивание файла — временный JWT (TTL `DocumentUrlTtlSeconds`, по умолчанию 5 мин),
  привязанный к конкретному `attachmentId`.
- Открыть на соредактирование можно только тот файл, который пользователю и так
  доступен (те же правила, что и на скачивание, — закрывает IDOR).
- Правка (`edit`) — только автору карточки или администратору (`ManageSystemSettings`);
  остальным — рецензирование и комментарии.
