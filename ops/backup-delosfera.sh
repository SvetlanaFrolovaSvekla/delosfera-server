#!/bin/bash
# Ежедневный бэкап Делосферы: база + файлы MinIO + конфиги. Без остановки сервисов.
set -euo pipefail

# PROJECT=/home/keremet/delosfera
# BACKUP_ROOT=/home/keremet/backups
# RETENTION_DAYS=14
PROJECT="${PROJECT:-/home/keremet/delosfera}"
BACKUP_ROOT="${BACKUP_ROOT:-/home/keremet/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"

STAMP=$(date +%Y-%m-%d_%H-%M)
TARGET="$BACKUP_ROOT/$STAMP"

echo "[$(date '+%F %T')] Бэкап в $TARGET"
mkdir -p "$TARGET"
cd "$PROJECT"

# База
docker compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$TARGET/database.dump"

# Файлы MinIO (низкий приоритет; код 1 = "file changed as we read it", это не ошибка)
nice -n 19 ionice -c3 tar czf "$TARGET/files.tar.gz" -C "$PROJECT" minio_data || [ $? -eq 1 ]

# Конфиги и пароли
tar czf "$TARGET/config.tar.gz" -C "$PROJECT" .env docker-compose.yml nginx.conf

# Проверки
gzip -t "$TARGET/files.tar.gz"
docker compose exec -T postgres pg_restore --list < "$TARGET/database.dump" > /dev/null

# Контрольные суммы
( cd "$TARGET" && sha256sum * > checksums.sha256 )
chown -R keremet:keremet "$TARGET"

echo "· готово, объём $(du -sh "$TARGET" | cut -f1)"

# Удалить копии старше RETENTION_DAYS дней (только папки вида 20xx-...)
find "$BACKUP_ROOT" -maxdepth 1 -type d -name '20*' -mtime +$RETENTION_DAYS -exec rm -rf {} +

echo "[$(date '+%F %T')] Завершено"
