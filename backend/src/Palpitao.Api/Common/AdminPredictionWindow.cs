using Palpitao.Api.Entities;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Common;

/// <summary>
/// When an admin may write predictions for a round — manual entry and OCR import alike.
/// Unlike the participant gate this one ignores the clock on purpose: the board often only
/// gets the WhatsApp screenshots in after the deadline, and nothing closes a round on its own,
/// so the admin's own "Finalize round" click is the real close. A finalized round has to be
/// reopened first, which keeps its scores from silently going stale.
/// </summary>
public static class AdminPredictionWindow
{
    public static void EnsureOpen(Round round)
    {
        switch (round.Status)
        {
            case RoundStatus.Published:
            case RoundStatus.Locked:
                return;
            case RoundStatus.Scored:
                throw new BusinessRuleException("adminPrediction.roundScored");
            case RoundStatus.Cancelled:
                throw new BusinessRuleException("adminPrediction.roundCancelled");
            default:
                throw new BusinessRuleException("adminPrediction.roundNotPublished");
        }
    }
}
