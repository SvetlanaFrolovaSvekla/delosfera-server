namespace delosfera_server.Modules.Analytics.DTO.Response.ProcessMining;

/// <summary>Сводный отчёт process mining по трейсам маршрутов согласования (MINE-1).</summary>
public class ProcessMiningReport
{
    public required ProcessMiningOverview Overview { get; set; }

    /// <summary>Фактические последовательности пройденных этапов и их частота.</summary>
    public required List<ProcessPathVariant> Variants { get; set; }

    /// <summary>Возвраты/доработки в разрезе этапов маршрута.</summary>
    public required List<ProcessReworkStep> Rework { get; set; }

    /// <summary>Узкие места в разрезе этапов маршрута.</summary>
    public required List<ProcessBottleneckStep> BottleneckSteps { get; set; }

    /// <summary>Узкие места в разрезе конкретных участников (согласующих).</summary>
    public required List<ProcessBottleneckParticipant> BottleneckParticipants { get; set; }

    /// <summary>Единица измерения длительностей: "раб.ч" (рабочие часы) или "ч" (астрономические).</summary>
    public required string DurationUnit { get; set; }
}

/// <summary>KPI-плашки process mining.</summary>
public class ProcessMiningOverview
{
    /// <summary>Всего запущенных маршрутов в выборке.</summary>
    public int TotalInstances { get; set; }

    /// <summary>Завершённых (есть дата завершения).</summary>
    public int CompletedInstances { get; set; }

    /// <summary>Идущих прямо сейчас.</summary>
    public int RunningInstances { get; set; }

    /// <summary>Маршрутов, где были возвраты/доработки (отклонение, вето, доработка).</summary>
    public int ReworkInstances { get; set; }

    /// <summary>Доля маршрутов с возвратами, %.</summary>
    public double ReworkRatePercent { get; set; }

    /// <summary>Число различных вариантов маршрута (уникальных последовательностей этапов).</summary>
    public int DistinctVariants { get; set; }

    /// <summary>Средняя длительность завершённого маршрута (в выбранных единицах).</summary>
    public double AverageDurationHours { get; set; }

    /// <summary>Медианная длительность завершённого маршрута (в выбранных единицах).</summary>
    public double MedianDurationHours { get; set; }
}

/// <summary>Вариант маршрута — фактическая последовательность пройденных этапов и её частота.</summary>
public class ProcessPathVariant
{
    /// <summary>Подпись варианта: этапы через « → ».</summary>
    public required string Signature { get; set; }

    /// <summary>Этапы варианта по порядку.</summary>
    public required List<string> Steps { get; set; }

    /// <summary>Сколько маршрутов прошло ровно по этой последовательности.</summary>
    public int Frequency { get; set; }

    /// <summary>Доля от всех маршрутов выборки, %.</summary>
    public double Percent { get; set; }

    /// <summary>Средняя длительность маршрутов этого варианта (завершённых), в выбранных единицах.</summary>
    public double AverageDurationHours { get; set; }

    /// <summary>Сколько маршрутов этого варианта прошли с возвратами/доработками.</summary>
    public int ReworkCount { get; set; }
}

/// <summary>Возвраты/доработки в разрезе этапа маршрута.</summary>
public class ProcessReworkStep
{
    public required string StepLabel { get; set; }

    /// <summary>Числовой код назначения этапа (StepKind) — для навигации/иконок на фронте.</summary>
    public int StepKind { get; set; }

    /// <summary>Сколько раз этот этап был реально пройден в выборке.</summary>
    public int Occurrences { get; set; }

    /// <summary>Сколько на этом этапе было возвратов (отклонение/вето).</summary>
    public int Returns { get; set; }

    /// <summary>Доля возвратов от числа прохождений этапа, %.</summary>
    public double ReturnRatePercent { get; set; }
}

/// <summary>Узкое место в разрезе этапа маршрута.</summary>
public class ProcessBottleneckStep
{
    public required string StepLabel { get; set; }
    public int StepKind { get; set; }

    /// <summary>Число решений участников, попавших в расчёт (активация→решение).</summary>
    public int Samples { get; set; }

    public double AverageWaitHours { get; set; }
    public double MedianWaitHours { get; set; }
    public double MaxWaitHours { get; set; }

    /// <summary>Доля решений, зачтённых автоакцептом по таймауту, %.</summary>
    public double TimeoutRatePercent { get; set; }
}

/// <summary>Узкое место в разрезе конкретного участника (согласующего).</summary>
public class ProcessBottleneckParticipant
{
    public int? UserId { get; set; }
    public required string Label { get; set; }
    public int Samples { get; set; }
    public double AverageWaitHours { get; set; }
    public double MedianWaitHours { get; set; }
}
