#!/usr/bin/env bash
#
# Резервное копирование СЭД «Делосфера» (NFR-02).
#
# Копируются две вещи: база данных и файловое хранилище. По отдельности они
# бесполезны — карточка без файла и файл без карточки одинаково не восстанавливают
# документ, поэтому копия делается одним запуском и складывается в один каталог
# с общей меткой времени.
#
# Запуск по расписанию (пример для cron, ежедневно в 01:30):
#   30 1 * * * /opt/delosfera/ops/backup.sh >> /var/log/delosfera-backup.log 2>&1

set -euo pipefail

BACKUP_ROOT="${BACKUP_ROOT:-/var/backups/delosfera}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGUSER="${PGUSER:-postgres}"
PGDATABASE="${PGDATABASE:-delosfera}"

MINIO_ALIAS="${MINIO_ALIAS:-delosfera}"
MINIO_BUCKET="${MINIO_BUCKET:-delosfera-vnd}"

STAMP="$(date +%Y-%m-%d_%H-%M)"
TARGET="${BACKUP_ROOT}/${STAMP}"

echo "[$(date '+%F %T')] Резервное копирование в ${TARGET}"
mkdir -p "${TARGET}"

# --- База данных ---------------------------------------------------------------
# Формат custom (-Fc), а не обычный SQL: он сжат и допускает выборочное
# восстановление отдельных таблиц, что при разборе инцидента важнее размера файла.
echo "· выгрузка базы ${PGDATABASE}"
pg_dump -h "${PGHOST}" -p "${PGPORT}" -U "${PGUSER}" -d "${PGDATABASE}" \
        -Fc -f "${TARGET}/database.dump"

# --- Файловое хранилище --------------------------------------------------------
# Вложения документов лежат в MinIO. Зеркалирование, а не копирование: повторный
# запуск не переливает то, что уже выгружено.
echo "· выгрузка файлов из ${MINIO_ALIAS}/${MINIO_BUCKET}"
if command -v mc >/dev/null 2>&1; then
    mc mirror --overwrite --quiet "${MINIO_ALIAS}/${MINIO_BUCKET}" "${TARGET}/files"
else
    echo "  ВНИМАНИЕ: клиент mc не установлен — файлы не выгружены" >&2
    echo "  Установите MinIO Client, иначе копия неполная и документ не восстановить" >&2
    exit 1
fi

# --- Контрольные суммы ---------------------------------------------------------
# Считаются сразу: испорченную копию нужно обнаружить сейчас, а не в тот момент,
# когда она понадобится.
echo "· контрольные суммы"
( cd "${TARGET}" && find . -type f -exec sha256sum {} \; > checksums.sha256 )

SIZE="$(du -sh "${TARGET}" | cut -f1)"
echo "· готово, объём ${SIZE}"

# --- Очистка старых копий ------------------------------------------------------
# Глубина поиска ограничена одним уровнем, а маска — каталогами копий: так под
# удаление не попадёт ничего, кроме собственных устаревших выгрузок.
echo "· удаление копий старше ${RETENTION_DAYS} дн."
find "${BACKUP_ROOT}" -maxdepth 1 -type d -name '20*' -mtime "+${RETENTION_DAYS}" -print -delete

echo "[$(date '+%F %T')] Резервное копирование завершено"
