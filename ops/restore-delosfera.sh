#!/bin/bash
#
# Восстановление Делосферы (docker compose) из копии, сделанной backup-delosfera.sh.
#
# ВНИМАНИЕ: заменяет текущую базу и файлы MinIO. Приложение на время
# восстановления останавливается (несколько минут). По расписанию не запускать. НЕ ЗАПУСКАТЬ НА ПРОДЕ БЕЗ НАСТОЯЩЕЙ АВАРИИ!
#
# Перед заменой скрипт сохраняет текущее состояние:
#   - дамп текущей базы  -> $BACKUP_ROOT/before-restore_<время>.dump
#   - текущую папку файлов -> $PROJECT/minio_data.before-restore_<время>
# так что неудачное восстановление можно откатить.
#
# Использование:
#   sudo ./restore-delosfera.sh /home/keremet/backups/2026-10-19_21-00

set -euo pipefail

PROJECT="${PROJECT:-/home/keremet/delosfera}"
BACKUP_ROOT="${BACKUP_ROOT:-/home/keremet/backups}"

SOURCE="${1:-}"
STAMP=$(date +%Y-%m-%d_%H-%M)

if [[ $EUID -ne 0 ]]; then
    echo "Запустите через sudo: файлы MinIO принадлежат root." >&2
    exit 1
fi

if [[ -z "$SOURCE" || ! -d "$SOURCE" ]]; then
    echo "Укажите папку копии: sudo $0 $BACKUP_ROOT/ГГГГ-ММ-ДД_ЧЧ-ММ" >&2
    exit 1
fi
SOURCE=$(cd "$SOURCE" && pwd)

for f in database.dump files.tar.gz; do
    if [[ ! -f "$SOURCE/$f" ]]; then
        echo "В копии нет файла $f — восстановление невозможно." >&2
        exit 1
    fi
done

cd "$PROJECT"

# --- Проверка целостности копии до любых изменений --------------------------------
if [[ -f "$SOURCE/checksums.sha256" ]]; then
    echo "· проверка контрольных сумм"
    ( cd "$SOURCE" && sha256sum --quiet -c checksums.sha256 )
else
    echo "  ВНИМАНИЕ: нет checksums.sha256, проверяю только архив" >&2
fi
gzip -t "$SOURCE/files.tar.gz"
docker compose exec -T postgres pg_restore --list < "$SOURCE/database.dump" > /dev/null

# --- Подтверждение ----------------------------------------------------------------
DB=$(docker compose exec -T postgres sh -c 'echo "$POSTGRES_DB"' | tr -d '\r')
echo
echo "Восстановление из: $SOURCE"
echo "База '$DB' и файлы MinIO будут ЗАМЕНЕНЫ содержимым копии."
echo "Приложение будет остановлено на время восстановления."
read -r -p "Для подтверждения введите имя базы ($DB): " CONFIRM
if [[ "$CONFIRM" != "$DB" ]]; then
    echo "Подтверждение не совпало — ничего не изменено." >&2
    exit 1
fi

# --- Страховочная копия текущего состояния ----------------------------------------
echo "· дамп текущей базы -> $BACKUP_ROOT/before-restore_$STAMP.dump"
mkdir -p "$BACKUP_ROOT"
docker compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' \
    > "$BACKUP_ROOT/before-restore_$STAMP.dump"

# --- Остановка приложения ---------------------------------------------------------
# postgres не останавливаем — он нужен для восстановления.
echo "· остановка backend, frontend, minio"
docker compose stop backend frontend minio

# --- База -------------------------------------------------------------------------
# Одна транзакция: при ошибке база останется в прежнем состоянии.
echo "· восстановление базы"
docker compose exec -T postgres sh -c \
    'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists --no-owner --single-transaction --exit-on-error' \
    < "$SOURCE/database.dump"

# --- Файлы MinIO ------------------------------------------------------------------
echo "· текущие файлы -> minio_data.before-restore_$STAMP"
mv "$PROJECT/minio_data" "$PROJECT/minio_data.before-restore_$STAMP"
echo "· распаковка файлов из копии"
tar xzf "$SOURCE/files.tar.gz" -C "$PROJECT"

# --- Запуск -----------------------------------------------------------------------
echo "· запуск minio, backend, frontend"
docker compose start minio backend frontend
sleep 5
docker compose ps

echo
echo "Восстановление завершено."
echo "Проверьте: вход в систему, открытие документа с вложением."
echo "Когда убедитесь, что всё в порядке, можно удалить страховочные копии:"
echo "  sudo rm -rf $PROJECT/minio_data.before-restore_$STAMP"
echo "  rm $BACKUP_ROOT/before-restore_$STAMP.dump"
echo "Конфиги (.env и др.) не восстанавливались — при необходимости они в $SOURCE/config.tar.gz"
