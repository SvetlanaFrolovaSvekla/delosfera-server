namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>Версия текста редакции — тройка файлов документа (RU/KG/EN). Файл в системе неизменяем:
/// любая замена загружает новый FileAttachment с новым Id, поэтому равные тройки означают один и тот
/// же текст. Ровно эти три файла входят в компонент "content" хэша резолюции
/// (ResolutionSignatureHasher.BuildContentDigest) — то есть именно их и подписывает согласующий.</summary>
public readonly record struct RedactionTextVersion(int RuFileId, int? KgFileId, int? EnFileId);

/// <summary>Снимок файлов редакции (VndRedactionRevisionSnapshot) в виде, нужном для
/// <see cref="FinalHoldCarryOver"/>: когда снят и какой текст действовал ДО этого момента.</summary>
public readonly record struct RedactionTextSnapshot(DateTime CreatedAt, RedactionTextVersion Text);

/// <summary>
/// Можно ли на финальной выдержке засчитать согласующему его прежнее чистое согласование, не
/// запрашивая подпись заново.
///
/// Лист согласования должен подтверждать, что ВСЕ согласующие подписали один и тот же итоговый текст.
/// Раньше при входе в финальную выдержку чистое согласование переносилось всем, кто не оставлял
/// замечаний, — даже если после их подписи инициатор заменил файлы документа при доработке. В листе
/// такой согласующий стоял под итоговой версией, хотя ЭЦП ставил под предыдущей.
///
/// Теперь перенос допустим, только если решение принималось по тому же тексту, что действует сейчас.
/// Если текст с тех пор меняли — у согласующего запрашивается решение (и ЭЦП) под итоговой версией
/// в рамках финальной выдержки, параллельно со всеми остальными. Чистая функция — без БД.
/// </summary>
public static class FinalHoldCarryOver
{
    /// <summary>Какой текст действовал в момент <paramref name="at"/>. Снимок
    /// (VndRedactionRevisionSnapshot) делается непосредственно ПЕРЕД перезаписью файлов при повторной
    /// отправке и хранит файлы, действовавшие до него, — поэтому текст на момент <paramref name="at"/>
    /// лежит в первом снимке, сделанном позже этого момента. Если таких снимков нет, файлы с тех пор не
    /// перезаписывались — действует текущий текст.</summary>
    public static RedactionTextVersion TextAt(
        DateTime at, RedactionTextVersion current, IEnumerable<RedactionTextSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots.OrderBy(s => s.CreatedAt))
        {
            if (snapshot.CreatedAt > at) return snapshot.Text;
        }

        return current;
    }

    /// <summary>Решение, принятое в момент <paramref name="decidedAt"/>, относится к тексту, который
    /// действует сейчас. Нет даты решения — подтвердить нельзя, считаем, что не относится (решение
    /// запросят заново: лишний запрос безопаснее, чем подпись под чужой версией в листе).</summary>
    public static bool DecidedOnCurrentText(
        DateTime? decidedAt, RedactionTextVersion current, IEnumerable<RedactionTextSnapshot> snapshots) =>
        decidedAt is { } at && TextAt(at, current, snapshots) == current;
}
