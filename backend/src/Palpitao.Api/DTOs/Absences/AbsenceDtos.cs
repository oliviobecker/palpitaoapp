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
