using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Palpitao.Application.Absences;
using Palpitao.Application.AdminPredictions;
using Palpitao.Application.Audit;
using Palpitao.Application.Auth;
using Palpitao.Application.Fixtures;
using Palpitao.Application.Flavio;
using Palpitao.Application.Groups;
using Palpitao.Application.Ocr;
using Palpitao.Application.Predictions;
using Palpitao.Application.Registrations;
using Palpitao.Application.Results;
using Palpitao.Application.Rounds;
using Palpitao.Application.Scoring;
using Palpitao.Application.Scouts;
using Palpitao.Application.Seasons;
using Palpitao.Application.Standings;
using Palpitao.Application.Teams;
using Palpitao.Application.Users;
using Palpitao.Domain.Scoring;

namespace Palpitao.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the use cases and the request validators. The ports they depend on (the
    /// interfaces under <c>Abstractions/</c>) are registered by the infrastructure and the web host.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Scoring
        services.AddSingleton<IScoringService, ScoringService>();
        services.AddScoped<ISeasonScoringConfigService, SeasonScoringConfigService>();
        services.AddScoped<IRoundScoringService, RoundScoringService>();
        services.AddScoped<IStandingsService, StandingsService>();

        // Accounts, groups and administration
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICurrentGroupService, CurrentGroupService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IRegistrationRequestService, RegistrationRequestService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IUserAdminService, UserAdminService>();

        // Seasons, rounds and predictions
        services.AddScoped<IRoundService, RoundService>();
        services.AddScoped<IRoundWeekService, RoundWeekService>();
        services.AddScoped<IPredictionsService, PredictionsService>();
        services.AddScoped<IAbsenceService, AbsenceService>();
        services.AddScoped<IFlavioRuleService, FlavioRuleService>();
        services.AddScoped<FlavioOverrideService>();
        services.AddScoped<ISeasonService, SeasonService>();
        services.AddScoped<IAdminPredictionService, AdminPredictionService>();

        // Prediction import (OCR)
        services.AddScoped<IPredictionImportService, PredictionImportService>();
        services.AddScoped<IOcrAliasService, OcrAliasService>();
        services.AddScoped<IOcrService, OcrService>();

        // Fixtures, teams and results
        services.AddScoped<IFixtureImportService, FixtureImportService>();
        services.AddScoped<ITeamCatalogService, TeamCatalogService>();
        services.AddScoped<IResultsUpdateService, ResultsUpdateService>();
        services.AddScoped<ITemporaryStandingsService, TemporaryStandingsService>();
        services.AddScoped<IPublicStandingsService, PublicStandingsService>();
        services.AddScoped<IScoutService, ScoutService>();

        return services;
    }
}
