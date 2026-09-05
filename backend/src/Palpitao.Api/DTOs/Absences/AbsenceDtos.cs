using Palpitao.Api.Enums;

namespace Palpitao.Api.DTOs.Absences;

public class AbsenceDto
{
    public Guid RoundId { get; set; }
    public int RoundNumber { get; set; }
    public Guid UserId { get; set; }
    public int AbsenceNumber { get; set; }
    public int PenaltyPoints { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AbsenceOverrideRequest
{
    public Guid UserId { get; set; }

    /// <summary>true = marcar como ausente, false = considerar presente.</summary>
    public bool IsAbsent { get; set; }

    public string Justification { get; set; } = string.Empty;
}

public class ReactivateRequest
{
    public string Justification { get; set; } = string.Empty;

    /// <summary>Rodadas encerradas em que o participante deve constar ausente.</summary>
    public List<Guid> AbsentRoundIds { get; set; } = [];
}

/// <summary>
/// Rodada já encerrada para palpites em que o participante pode constar ausente ao ser
/// (re)ativado: ele não completou os palpites e ainda não há override marcando ausência.
/// </summary>
public class AbsenceCandidateRoundDto
{
    public Guid RoundId { get; set; }

    public int Number { get; set; }

    public string? Title { get; set; }

    public RoundStatus Status { get; set; }

    public int MatchCount { get; set; }

    public int PredictionCount { get; set; }

    /// <summary>
    /// Ausências só se materializam na pontuação, então uma rodada já pontuada exige
    /// repontuar/recalcular para o override mudar alguma coisa.
    /// </summary>
    public bool RequiresRescore => Status == RoundStatus.Scored;

    /// <summary>Já existe um override marcando o participante como presente; confirmar substitui.</summary>
    public bool HasPresentOverride { get; set; }
}

/// <summary>
/// Rodada já encerrada para palpites (Locked/Scored) da temporada ativa em que o participante
/// hoje consta ausente, ou em que existe um override — o conjunto que o admin revisa para
/// abonar (ou restaurar) ausências.
/// </summary>
public class AbsenceReviewRoundDto
{
    public Guid RoundId { get; set; }

    public int Number { get; set; }

    public string? Title { get; set; }

    public RoundStatus Status { get; set; }

    public int MatchCount { get; set; }

    public int PredictionCount { get; set; }

    /// <summary>Estado efetivo hoje: override, senão "palpites incompletos".</summary>
    public bool IsAbsent { get; set; }

    public bool HasOverride { get; set; }

    /// <summary>
    /// Ausências só se materializam na pontuação, então mudar uma rodada já pontuada exige
    /// recalcular a temporada para renumerar a escada de todo mundo.
    /// </summary>
    public bool RequiresRecalculation => Status == RoundStatus.Scored;

    /// <summary>Ordinal já gravado na escada (linha de <c>Absence</c>), se houver.</summary>
    public int? AbsenceNumber { get; set; }

    /// <summary>Punição já gravada para essa ausência, se houver.</summary>
    public int? PenaltyPoints { get; set; }
}

/// <summary>Decisão explícita do admin para uma rodada exibida na revisão.</summary>
public class AbsenceReviewDecision
{
    public Guid RoundId { get; set; }

    /// <summary>true = continua/volta a contar como ausente, false = considerar presente.</summary>
    public bool IsAbsent { get; set; }
}

public class AbsenceReviewRequest
{
    public string Justification { get; set; } = string.Empty;

    /// <summary>
    /// Uma decisão por rodada que o admin viu. Rodadas não mencionadas ficam como estão, então
    /// uma rodada que fechou entre a listagem e o envio nunca é virada por omissão.
    /// </summary>
    public List<AbsenceReviewDecision> Rounds { get; set; } = [];
}

public class AbsenceReviewResultDto
{
    /// <summary>Rodadas cujo estado efetivo mudou (um override gravado por rodada).</summary>
    public int ChangedRounds { get; set; }

    /// <summary>A temporada foi recalculada na mesma transação (alguma rodada pontuada mudou).</summary>
    public bool Recalculated { get; set; }
}
