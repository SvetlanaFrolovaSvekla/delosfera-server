#!/usr/bin/env bash
#
# Восстановление СЭД «Делосфера» из резервной копии (NFR-02).
#
# Скрипт намеренно требует подтверждения и не запускается по расписанию:
# восстановление замещает текущие данные, и единственная ситуация, когда это
# уместно, — осознанное решение человека.
#
# Использование:
#   ops/restore.sh /var/backups/delosfera/2026-08-12_01-30

set -euo pipefail

SOURCE="${1:-}"

if [[ -z "${SOURCE}" || ! -d "${SOURCE}" ]]; then
    echo "Укажите каталог копии: ops/restore.sh /var/backups/delosfera/ГГГГ-ММ-ДД_ЧЧ-ММ" >&2
    exit 1
fi

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGUSER="${PGUSER:-postgres}"
PGDATABASE="${PGDATABASE:-delosfera}"

MINIO_ALIAS="${MINIO_ALIAS:-delosfera}"
MINIO_BUCKET="${MINIO_BUCKET:-delosfera-vnd}"

echo "Восстановление из ${SOURCE}"
echo "База: ${PGDATABASE} на ${PGHOST}:${PGPORT} — текущее содержимое будет замещено."
read -r -p "Введите имя базы для подтверждения: " CONFIRM

if [[ "${CONFIRM}" != "${PGDATABASE}" ]]; then
    echo "Подтверждение не совпало — восстановление отменено." >&2
    exit 1
fi

# --- Проверка целостности копии ------------------------------------------------
# Делается до того, как что-то трогать: восстановление из битой копии оставит
# систему в худшем состоянии, чем она была.
if [[ -f "${SOURCE}/checksums.sha256" ]]; then
    echo "· проверка контрольных сумм"
    ( cd "${SOURCE}" && sha256sum --quiet -c checksums.sha256 )
else
    echo "  ВНИМАНИЕ: файла контрольных сумм нет, целостность копии не проверена" >&2
fi

# --- Остановка приложения ------------------------------------------------------
# Приложение должно быть остановлено: восстановление под работающим сервисом даёт
# наполовину старые, наполовину новые данные.
if systemctl list-units --type=service 2>/dev/null | grep -q delosfera; then
    echo "· остановка сервиса delosfera"
    systemctl stop delosfera
fi

# --- База данных ---------------------------------------------------------------
echo "· восстановление базы"
pg_restore -h "${PGHOST}" -p "${PGPORT}" -U "${PGUSER}" -d "${PGDATABASE}" \
           --clean --if-exists --no-owner "${SOURCE}/database.dump"

# --- Файлы ---------------------------------------------------------------------
if [[ -d "${SOURCE}/files" ]]; then
    echo "· восстановление файлов"
    mc mirror --overwrite --remove --quiet "${SOURCE}/files" "${MINIO_ALIAS}/${MINIO_BUCKET}"
else
    echo "  ВНИМАНИЕ: файлов в копии нет — вложения документов не восстановлены" >&2
fi

if systemctl list-units --type=service 2>/dev/null | grep -q delosfera; then
    echo "· запуск сервиса delosfera"
    systemctl start delosfera
fi

echo "Восстановление завершено."
echo "Проверьте: вход в систему, открытие документа с вложением, выгрузку любого отчёта."
