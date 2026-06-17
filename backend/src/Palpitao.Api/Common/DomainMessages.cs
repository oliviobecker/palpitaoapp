namespace Palpitao.Api.Common;

/// <summary>
/// Single source of truth for user-facing messages (domain errors, auth, generic
/// errors). Keys are stable identifiers; the Portuguese text is also the fallback
/// used for logs and <see cref="Exception.Message"/>. The middleware resolves the
/// key to the request language (Accept-Language) for the HTTP response.
/// </summary>
public static class DomainMessages
{
    public static readonly IReadOnlyDictionary<string, (string Pt, string En)> Catalog =
        new Dictionary<string, (string Pt, string En)>
        {
            // Generic / auth (previously in LocalizationService).
            ["error.unexpected"] = ("Ocorreu um erro inesperado. Tente novamente.", "An unexpected error occurred. Please try again."),
            ["error.notFound"] = ("Recurso não encontrado.", "Resource not found."),
            ["error.unauthorized"] = ("Não autorizado.", "Unauthorized."),
            ["auth.invalidCredentials"] = ("E-mail ou senha inválidos.", "Invalid e-mail or password."),
            ["auth.inactiveUser"] = ("Usuário inativo. Procure o administrador.", "Inactive user. Contact the administrator."),
            ["auth.registrationSubmitted"] = (
                "Cadastro enviado com sucesso. Aguarde a aprovação do administrador para acessar o sistema.",
                "Registration submitted successfully. Please wait for admin approval before accessing the system."),
            ["auth.pendingApproval"] = (
                "Seu cadastro ainda está pendente de aprovação.",
                "Your registration is still pending approval."),
            ["auth.rejected"] = (
                "Seu cadastro foi rejeitado. Entre em contato com o administrador.",
                "Your registration was rejected. Please contact the administrator."),
            ["auth.accountInactive"] = (
                "Sua conta está inativa. Entre em contato com o administrador.",
                "Your account is inactive. Please contact the administrator."),
            ["auth.passwordMismatch"] = (
                "A confirmação de senha não confere.",
                "The password confirmation does not match."),
            ["auth.weakPassword"] = (
                "A senha deve ter no mínimo 8 caracteres, com ao menos uma letra e um número.",
                "The password must be at least 8 characters long and include at least one letter and one number."),
            ["registration.notPending"] = (
                "A solicitação não está pendente de aprovação.",
                "The request is not pending approval."),

            // Groups (multi-tenant).
            ["group.accessDenied"] = (
                "Você não tem acesso a este grupo.",
                "You do not have access to this group."),
            ["group.headerMissing"] = (
                "Selecione um grupo para continuar.",
                "Select a group to continue."),
            ["group.adminOnly"] = (
                "Apenas administradores do grupo podem realizar esta ação.",
                "Only group administrators can perform this action."),
            ["group.nameRequired"] = (
                "O nome do grupo é obrigatório.",
                "The group name is required."),
            ["group.slugExists"] = (
                "Já existe um grupo com esse nome.",
                "A group with this name already exists."),
            ["group.required"] = (
                "Selecione o grupo desejado.",
                "Select the desired group."),
            ["group.notFound"] = (
                "Grupo não encontrado.",
                "Group not found."),
            ["group.alreadyMember"] = (
                "Você já solicitou acesso ou já participa deste grupo.",
                "You have already requested access to or are a member of this group."),
            ["group.requestSubmitted"] = (
                "Solicitação enviada com sucesso. Aguarde a aprovação do administrador do grupo.",
                "Request submitted successfully. Please wait for the group administrator approval."),
            ["group.created"] = (
                "Grupo criado com sucesso. Você já pode acessar como administrador.",
                "Group created successfully. You can now sign in as administrator."),

            // Not found.
            ["notFound.participant"] = ("Participante não encontrado.", "Participant not found."),
            ["notFound.round"] = ("Rodada não encontrada.", "Round not found."),
            ["notFound.user"] = ("Usuário não encontrado.", "User not found."),
            ["notFound.match"] = ("Jogo não encontrado.", "Match not found."),
            ["notFound.season"] = ("Temporada não encontrada.", "Season not found."),
            ["notFound.group"] = ("Grupo não encontrado.", "Group not found."),
            ["notFound.team"] = ("Time informado não existe.", "The given team does not exist."),
            ["notFound.ocrBatch"] = ("Importação não encontrada.", "Import not found."),
            ["notFound.ocrCandidate"] = ("Candidato não encontrado.", "Candidate not found."),

            // Common.
            ["common.justificationRequired"] = ("A justificativa é obrigatória.", "A justification is required."),
            ["validation.required"] = ("Preencha os campos obrigatórios.", "Please fill in the required fields."),

            // Input validation (FluentValidation message keys).
            ["validation.name.required"] = ("O nome é obrigatório.", "The name is required."),
            ["validation.name.length"] = ("Informe um nome válido.", "Enter a valid name."),
            ["validation.email.required"] = ("O e-mail é obrigatório.", "The e-mail is required."),
            ["validation.email.invalid"] = ("Informe um e-mail válido.", "Enter a valid e-mail."),
            ["validation.password.required"] = ("A senha é obrigatória.", "The password is required."),
            ["validation.password.min6"] = ("A senha deve ter ao menos 6 caracteres.", "The password must be at least 6 characters long."),
            ["validation.passwordConfirm.required"] = ("Confirme a senha.", "Please confirm the password."),
            ["validation.group.nameRequired"] = ("O nome do grupo é obrigatório.", "The group name is required."),
            ["validation.group.nameLength"] = ("Informe um nome de grupo válido.", "Enter a valid group name."),
            ["validation.adminName.required"] = ("O nome do administrador é obrigatório.", "The administrator name is required."),
            ["tournamentType.required"] = ("Escolha o tipo do certame.", "Choose the tournament type."),
            ["validation.group.required"] = ("Selecione o grupo desejado.", "Select the desired group."),
            ["validation.season.required"] = ("A temporada é obrigatória.", "The season is required."),
            ["validation.seasonName.required"] = ("O nome da temporada é obrigatório.", "The season name is required."),
            ["validation.startDate.required"] = ("A data inicial é obrigatória.", "The start date is required."),
            ["validation.endDate.required"] = ("A data final é obrigatória.", "The end date is required."),
            ["validation.roundNumber.min"] = ("O número da rodada deve ser maior que zero.", "The round number must be greater than zero."),
            ["validation.competition.required"] = ("A competição é obrigatória.", "The competition is required."),
            ["validation.phase.required"] = ("A fase é obrigatória.", "The phase is required."),
            ["validation.homeTeam.required"] = ("O mandante é obrigatório.", "The home team is required."),
            ["validation.awayTeam.required"] = ("O visitante é obrigatório.", "The away team is required."),
            ["validation.startsAt.required"] = ("A data/hora do jogo é obrigatória.", "The match date/time is required."),
            ["validation.externalId.required"] = ("O identificador externo é obrigatório.", "The external id is required."),
            ["validation.participant.required"] = ("O participante é obrigatório.", "The participant is required."),
            ["validation.predictions.required"] = ("Envie os palpites da rodada.", "Submit the round predictions."),
            ["validation.match.required"] = ("O jogo é obrigatório.", "The match is required."),
            ["validation.score.negative"] = ("O placar não pode ser negativo.", "The score cannot be negative."),
            ["validation.justification.required"] = ("A justificativa é obrigatória.", "A justification is required."),

            // Users.
            ["user.emailExists"] = ("Já existe um usuário com esse e-mail.", "A user with this e-mail already exists."),

            // Participant state.
            ["participant.inactive"] = ("Participante inativo.", "Inactive participant."),

            // Rounds.
            ["round.duplicateNumber"] = ("Já existe uma rodada com esse número nesta temporada.", "A round with this number already exists in this season."),
            ["round.cannotEditClosed"] = ("Não é possível editar uma rodada bloqueada, pontuada ou cancelada.", "Cannot edit a locked, scored or cancelled round."),
            ["round.onlyDraftPublished"] = ("Apenas rodadas em rascunho podem ser publicadas.", "Only draft rounds can be published."),
            ["round.needsMatchToPublish"] = ("A rodada precisa de pelo menos um jogo para ser publicada.", "The round needs at least one match to be published."),
            ["round.onlyPublishedLocked"] = ("Apenas rodadas publicadas podem ser bloqueadas.", "Only published rounds can be locked."),
            ["round.cannotCancelScored"] = ("Não é possível cancelar uma rodada já pontuada.", "Cannot cancel a round that has already been scored."),
            ["round.alreadyCancelled"] = ("A rodada já está cancelada.", "The round is already cancelled."),
            ["round.mustBeLockedToScore"] = ("A rodada precisa estar bloqueada para ser calculada.", "The round must be locked to be scored."),
            ["round.noMatches"] = ("A rodada não possui jogos.", "The round has no matches."),
            ["round.allResultsRequired"] = ("Cadastre o resultado de todos os jogos antes de calcular a rodada.", "Enter the result of every match before scoring the round."),
            ["round.cannotEditMatchClosedNoJustification"] = (
                "Não é possível alterar jogos de uma rodada bloqueada, pontuada ou cancelada sem justificativa administrativa.",
                "Cannot change matches of a locked, scored or cancelled round without an administrative justification."),

            // Matches.
            ["match.competitionRequired"] = ("A competição é obrigatória.", "The competition is required."),
            ["match.phaseRequired"] = ("A fase é obrigatória.", "The phase is required."),
            ["match.dateRequired"] = ("A data/hora do jogo é obrigatória.", "The match date/time is required."),
            ["match.sameTeam"] = ("Mandante e visitante não podem ser o mesmo time.", "Home and away cannot be the same team."),
            ["match.multiplierJustificationRequired"] = ("É necessário justificar o multiplicador manual.", "A justification is required for the manual multiplier."),
            ["match.leagueOneSingle"] = (
                "A rodada não pode ter mais de um jogo da League One sem override manual justificado.",
                "The round cannot have more than one League One match without a justified manual override."),

            // Rounds (period / fixtures).
            ["round.startEndRequired"] = (
                "Informe a data de início e a data de fim da rodada.",
                "Provide both the round start date and end date."),

            // Fixtures (external import).
            ["fixtures.fetchFailed"] = (
                "Não foi possível buscar os jogos da fonte externa no momento.",
                "Could not fetch fixtures from the external source at this time."),
            ["fixtures.noneFound"] = (
                "Nenhum jogo encontrado para o período selecionado.",
                "No fixtures found for the selected period."),
            ["fixtures.selectNone"] = (
                "Selecione pelo menos um jogo para adicionar à rodada.",
                "Select at least one match to add to the round."),
            ["fixtures.endBeforeStart"] = (
                "A data final deve ser maior ou igual à data inicial.",
                "End date must be greater than or equal to start date."),
            ["fixtures.leagueOneSingle"] = (
                "Esta rodada já possui um jogo da League One. Para adicionar outro, informe uma justificativa.",
                "This round already has one League One match. To add another, provide a justification."),
            ["fixtures.importDisabled"] = (
                "A importação externa de jogos está desabilitada.",
                "External fixture import is disabled."),

            // Results / scoring.
            ["result.cannotRegister"] = ("Não é possível cadastrar resultado nesta rodada.", "Cannot register a result for this round."),
            ["results.refreshed"] = ("Resultados atualizados com sucesso.", "Results refreshed successfully."),
            ["results.providerDisabled"] = (
                "Nenhum provedor de resultados externo está ativo. A classificação temporária foi recalculada com os resultados atuais.",
                "No external results provider is active. The temporary standings were recalculated from the current results."),
            ["results.fetchFailed"] = (
                "Não foi possível buscar os resultados da fonte externa no momento.",
                "Could not fetch results from the external source at this time."),
            ["results.roundCancelled"] = ("Não é possível atualizar resultados de uma rodada cancelada.", "Cannot refresh results of a cancelled round."),
            ["results.roundScored"] = ("A rodada já foi pontuada oficialmente.", "The round has already been officially scored."),
            ["results.roundNotPublished"] = ("Publique a rodada antes de atualizar os resultados.", "Publish the round before refreshing results."),

            // Predictions.
            ["prediction.negativeScore"] = ("O placar não pode ser negativo.", "The score cannot be negative."),
            ["prediction.allMatchesRequired"] = ("Envie os palpites de todos os jogos da rodada.", "Send predictions for every match in the round."),
            ["prediction.noDuplicates"] = ("Não envie palpites duplicados para o mesmo jogo.", "Do not send duplicate predictions for the same match."),
            ["prediction.matchNotInRound"] = ("Um dos jogos informados não pertence à rodada.", "One of the given matches does not belong to the round."),
            ["prediction.inactiveCannotPredict"] = ("Participante inativo não pode palpitar.", "An inactive participant cannot predict."),
            ["prediction.eliminatedCannotPredict"] = ("Participante eliminado não pode palpitar.", "An eliminated participant cannot predict."),
            ["prediction.roundNotOpenYet"] = ("A rodada ainda não está aberta para palpites.", "The round is not open for predictions yet."),
            ["prediction.roundLocked"] = ("A rodada está bloqueada. Não é mais possível palpitar.", "The round is locked. Predictions are no longer allowed."),
            ["prediction.roundScored"] = ("A rodada já foi pontuada.", "The round has already been scored."),
            ["prediction.roundCancelled"] = ("A rodada foi cancelada.", "The round was cancelled."),
            ["prediction.deadlinePassed"] = ("O prazo para palpitar nesta rodada já encerrou.", "The deadline to predict for this round has passed."),

            // Mirror.
            ["mirror.afterLockOnly"] = ("O espelho fica disponível somente após o bloqueio da rodada.", "The mirror is only available after the round is locked."),
            ["mirror.notAllowed"] = ("Você não tem permissão para visualizar os palpites dos demais participantes.", "You do not have permission to view other participants' predictions."),

            // Admin manual predictions.
            ["adminPrediction.eliminatedNeedsOverride"] = (
                "Participante eliminado. Use override com justificativa para registrar.",
                "Eliminated participant. Use an override with justification to register."),
            ["adminPrediction.alreadyHasPredictions"] = ("O participante já possui palpites. Confirme a substituição.", "The participant already has predictions. Confirm the replacement."),
            ["adminPrediction.roundNotOpenOverride"] = (
                "A rodada não está aberta para palpites. Use override com justificativa para registrar fora do prazo.",
                "The round is not open for predictions. Use an override with justification to register after the deadline."),

            // OCR.
            ["ocr.sendImage"] = ("Envie uma imagem.", "Send an image."),
            ["ocr.invalidFormat"] = ("Envie uma imagem nos formatos PNG, JPG, JPEG ou WEBP.", "Send an image in PNG, JPG, JPEG or WEBP format."),
            ["ocr.emptyFile"] = ("Arquivo de imagem vazio.", "Empty image file."),
            ["ocr.tooLarge"] = ("A imagem deve ter no máximo 10 MB.", "The image must be at most 10 MB."),
            ["ocr.incompleteCandidates"] = ("Há candidatos incompletos. Revise antes de confirmar a importação.", "There are incomplete candidates. Review before confirming the import."),
            ["ocr.processFailed"] = (
                "Não foi possível processar a imagem com OCR. Verifique os arquivos de idioma (tessdata).",
                "Could not process the image with OCR. Check the language files (tessdata)."),

            // Flávio rule.
            ["flavio.insufficientData"] = ("Rodada sem dados suficientes para a Regra Flávio.", "Round without enough data for the Flávio rule."),

            // Seasons.
            ["season.endBeforeStart"] = ("A data final não pode ser anterior à data inicial.", "The end date cannot be earlier than the start date."),
        };

    /// <summary>Resolves a key to the given language ("pt"/"en"); falls back to the key itself.</summary>
    public static string Resolve(string key, string lang)
        => Catalog.TryGetValue(key, out var value) ? (lang == "pt" ? value.Pt : value.En) : key;

    /// <summary>Portuguese text for a key (used for logs and <see cref="Exception.Message"/>).</summary>
    public static string Pt(string key) => Resolve(key, "pt");
}
