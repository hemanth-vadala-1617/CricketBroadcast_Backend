using CricketStream.Application.Common;
using CricketStream.Application.Features.Matches;
using CricketStream.Application.Interfaces;
using CricketStream.Contracts.Graphics;
using CricketStream.Contracts.Matches;
using CricketStream.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CricketStream.Application.Features.Graphics;

public interface IGraphicsService {
    Task<Result<MatchStateDto>> SetAsync(Guid matchId, string key, SetGraphicRequest request);
}

public class GraphicsService : IGraphicsService {
    private readonly ICricketDbContext _db;
    private readonly MatchCommitter _committer;
    public GraphicsService(ICricketDbContext db, MatchCommitter committer) { _db = db; _committer = committer; }

    public async Task<Result<MatchStateDto>> SetAsync(Guid matchId, string key, SetGraphicRequest request) {
        var canonical = MatchStateBuilder.GraphicKeys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        if (canonical is null)
            return Result<MatchStateDto>.Fail(Fail.Validation($"Unknown graphic '{key}'. Valid: {string.Join(", ", MatchStateBuilder.GraphicKeys)}."));

        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match is null) return Result<MatchStateDto>.Fail(Fail.NotFound("Match not found."));

        var payload = request.Payload is { ValueKind: not System.Text.Json.JsonValueKind.Null and not System.Text.Json.JsonValueKind.Undefined } p ? p.GetRawText() : null;
        if (canonical == "Banner" && request.IsVisible) {
            var hasContent = request.Payload is { ValueKind: System.Text.Json.JsonValueKind.Object } o &&
                ((o.TryGetProperty("text", out var t) && !string.IsNullOrWhiteSpace(t.GetString())) ||
                 (o.TryGetProperty("imageUrl", out var i) && !string.IsNullOrWhiteSpace(i.GetString())));
            if (!hasContent) return Result<MatchStateDto>.Fail(Fail.Validation("A banner needs text or an image."));
        }

        var row = await _db.OverlayGraphicStates.FirstOrDefaultAsync(g => g.MatchId == matchId && g.GraphicKey == canonical);
        if (row is null) {
            row = new OverlayGraphicState { MatchId = matchId, GraphicKey = canonical };
            _db.OverlayGraphicStates.Add(row);
        }
        row.IsVisible = request.IsVisible;
        if (payload is not null || !request.IsVisible) row.PayloadJson = payload ?? row.PayloadJson;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        return await _committer.CommitAsync(match);
    }
}

