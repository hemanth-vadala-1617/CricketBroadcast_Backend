using CricketStream.Contracts.Graphics;
using CricketStream.Contracts.Master;
using CricketStream.Contracts.Matches;
using CricketStream.Contracts.Players;
using CricketStream.Contracts.Revisions;
using CricketStream.Contracts.Scoring;
using CricketStream.Contracts.Teams;
using CricketStream.Domain.Enums;
using FluentValidation;

namespace CricketStream.Application.Features;

public static class ValidationExtensions {
    /// <summary>First validation message, or null when the request is valid.</summary>
    public static async Task<string?> FirstErrorAsync<T>(this IValidator<T> v, T request) {
        var r = await v.ValidateAsync(request);
        return r.IsValid ? null : r.Errors[0].ErrorMessage;
    }
}

public class ScoreBallRequestValidator : AbstractValidator<ScoreBallRequest> {
    public ScoreBallRequestValidator() {
        RuleFor(x => x.ClientRequestId).NotEmpty().WithMessage("clientRequestId is required (idempotency key).");
        RuleFor(x => x.RunsBat).InclusiveBetween(0, 7);
        RuleFor(x => x.ExtraRuns).InclusiveBetween(0, 7);
        RuleFor(x => x.ExtrasType).Must(t => Enum.TryParse<ExtrasType>(t, true, out _)).WithMessage("Unknown extras type.");
        RuleFor(x => x.SecondaryExtrasType).Must(t => Enum.TryParse<ExtrasType>(t, true, out _)).WithMessage("Unknown secondary extras type.");
        When(x => x.Wicket is not null, () =>
            RuleFor(x => x.Wicket!.DismissalType).Must(t => Enum.TryParse<DismissalType>(t, true, out _)).WithMessage("Unknown dismissal type."));
    }
}
public class EditBallRequestValidator : AbstractValidator<EditBallRequest> {
    public EditBallRequestValidator() {
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(3).WithMessage("Give a reason (min 3 characters).");
        RuleFor(x => x.Ball).SetValidator(new ScoreBallRequestValidator());
    }
}
public class UndoBallRequestValidator : AbstractValidator<UndoBallRequest> {
    public UndoBallRequestValidator() { RuleFor(x => x.Reason).NotEmpty().MinimumLength(3).WithMessage("Give a reason (min 3 characters)."); }
}
public class CreateMatchRequestValidator : AbstractValidator<CreateMatchRequest> {
    public CreateMatchRequestValidator() {
        RuleFor(x => x.TournamentId).NotEmpty();
        RuleFor(x => x.HomeTeamId).NotEmpty();
        RuleFor(x => x.AwayTeamId).NotEmpty().NotEqual(x => x.HomeTeamId).WithMessage("Home and away teams must be different.");
        RuleFor(x => x.VenueId).NotEmpty();
        RuleFor(x => x.MatchRulesId).NotEmpty();
        RuleFor(x => x.MatchFormat).Must(f => Enum.TryParse<MatchFormat>(f, true, out _)).WithMessage("Unknown match format.");
        RuleFor(x => x.Title).MaximumLength(200);
    }
}
public class StartInningsRequestValidator : AbstractValidator<StartInningsRequest> {
    public StartInningsRequestValidator() {
        RuleFor(x => x.StrikerId).NotEmpty();
        RuleFor(x => x.NonStrikerId).NotEmpty().NotEqual(x => x.StrikerId).WithMessage("Striker and non-striker must be different players.");
        RuleFor(x => x.BowlerId).NotEmpty();
    }
}
public class SetSquadRequestValidator : AbstractValidator<SetSquadRequest> {
    public SetSquadRequestValidator() {
        RuleFor(x => x.TeamId).NotEmpty();
        RuleFor(x => x.Players).NotEmpty().WithMessage("Pick at least one player.");
        RuleFor(x => x.Players).Must(p => p.Select(i => i.PlayerId).Distinct().Count() == p.Count).WithMessage("A player is listed twice.");
        RuleFor(x => x.Players).Must(p => p.Count(i => i.IsPlayingXI) <= 11).WithMessage("A playing XI has at most 11 players.");
        RuleFor(x => x.Players).Must(p => p.Count <= 25).WithMessage("A squad has at most 25 players.");
        RuleFor(x => x.Players).Must(p => p.Count(i => i.IsCaptain) <= 1).WithMessage("Only one captain.");
        RuleFor(x => x.Players).Must(p => p.Count(i => i.IsWicketKeeper) <= 1).WithMessage("Only one wicket-keeper.");
        RuleFor(x => x.Staff).Must(s => s.Count <= 15).WithMessage("At most 15 support staff per team.");
        RuleForEach(x => x.Staff).ChildRules(s => {
            s.RuleFor(i => i.Name).NotEmpty().WithMessage("Each support staff member needs a name.").MaximumLength(100);
            s.RuleFor(i => i.Role).NotEmpty().WithMessage("Each support staff member needs a role.").MaximumLength(60);
        });
    }
}
public class WinProbabilityRequestValidator : AbstractValidator<WinProbabilityRequest> {
    public WinProbabilityRequestValidator() {
        RuleFor(x => x.Home).InclusiveBetween(0, 100);
        RuleFor(x => x.Draw).InclusiveBetween(0, 100);
        RuleFor(x => x.Away).InclusiveBetween(0, 100);
        RuleFor(x => x).Must(x => x.Home + x.Draw + x.Away is 0 or 100).WithMessage("Percentages must add up to 100 (or all be 0 to hide).");
    }
}
public class CreateTeamRequestValidator : AbstractValidator<CreateTeamRequest> {
    public CreateTeamRequestValidator() {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ShortName).NotEmpty().MaximumLength(6);
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.PrimaryColor!).Matches("^#[0-9a-fA-F]{6}$").When(x => !string.IsNullOrEmpty(x.PrimaryColor)).WithMessage("Colour must look like #1a2b3c.");
        RuleFor(x => x.SecondaryColor!).Matches("^#[0-9a-fA-F]{6}$").When(x => !string.IsNullOrEmpty(x.SecondaryColor)).WithMessage("Colour must look like #1a2b3c.");
    }
}
public class PlayerDtoValidator : AbstractValidator<PlayerDto> {
    public PlayerDtoValidator() {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ShortName).NotEmpty().MaximumLength(20);
        RuleFor(x => x.PlayerRole).Must(r => Enum.TryParse<PlayerRole>(r, true, out _)).WithMessage("Unknown player role.");
        RuleFor(x => x.CareerLabel).MaximumLength(20);
        RuleFor(x => x.CareerMatches).GreaterThanOrEqualTo(0); RuleFor(x => x.CareerRuns).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CareerFifties).GreaterThanOrEqualTo(0); RuleFor(x => x.CareerHundreds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CareerAverage).InclusiveBetween(0m, 1000m); RuleFor(x => x.CareerStrikeRate).InclusiveBetween(0m, 1000m);
    }
}
public class VenueDtoValidator : AbstractValidator<VenueDto> {
    public VenueDtoValidator() { RuleFor(x => x.Name).NotEmpty().MaximumLength(150); RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0); }
}
public class TournamentDtoValidator : AbstractValidator<TournamentDto> {
    public TournamentDtoValidator() {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ShortName).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Format).Must(f => Enum.TryParse<MatchFormat>(f, true, out _)).WithMessage("Unknown format.");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("End date is before start date.");
    }
}
public class MatchRulesDtoValidator : AbstractValidator<MatchRulesDto> {
    public MatchRulesDtoValidator() {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BallsPerOver).InclusiveBetween(1, 12);
        RuleFor(x => x.OversPerInnings).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxOversPerBowler).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PowerplayOvers).GreaterThanOrEqualTo(0);
        RuleFor(x => x.WideRuns).InclusiveBetween(0, 5);
        RuleFor(x => x.NoBallRuns).InclusiveBetween(0, 5);
        RuleFor(x => x.InningsPerSide).InclusiveBetween(1, 2);
        RuleFor(x => x.Days).InclusiveBetween(1, 7);
        RuleFor(x => x.OversPerDay).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SessionsPerDay).InclusiveBetween(1, 4);
        RuleFor(x => x).Must(x => x.IsTest || x.OversPerInnings > 0).WithMessage("Limited-overs rules need overs per innings.");
    }
}
public class OverlayThemeDtoValidator : AbstractValidator<OverlayThemeDto> {
    private const string Hex = "^#[0-9a-fA-F]{6}$";
    public OverlayThemeDtoValidator() {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PrimaryColor).Matches(Hex).WithMessage("Colour must look like #1a2b3c.");
        RuleFor(x => x.SecondaryColor).Matches(Hex).WithMessage("Colour must look like #1a2b3c.");
        RuleFor(x => x.AccentColor).Matches(Hex).WithMessage("Colour must look like #1a2b3c.");
    }
}
public class SetGraphicRequestValidator : AbstractValidator<SetGraphicRequest> { }
