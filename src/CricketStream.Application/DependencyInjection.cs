using CricketStream.Application.Features;
using CricketStream.Application.Features.Graphics;
using CricketStream.Application.Features.Master;
using CricketStream.Application.Features.Matches;
using CricketStream.Application.Features.Revisions;
using CricketStream.Application.Features.Scoring;
using CricketStream.Application.Features.Teams;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
namespace CricketStream.Application;
public static class DependencyInjection {
    public static IServiceCollection AddApplication(this IServiceCollection services) {
        services.AddValidatorsFromAssemblyContaining<ScoreBallRequestValidator>();
        services.AddScoped<IMatchStateService, MatchStateService>();
        services.AddScoped<MatchCommitter>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IMasterDataService, MasterDataService>();
        services.AddScoped<IMatchService, MatchService>();
        services.AddScoped<IScoringService, ScoringService>();
        services.AddScoped<IRevisionService, RevisionService>();
        services.AddScoped<IGraphicsService, GraphicsService>();
        return services;
    }
}

