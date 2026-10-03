using CricketStream.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CricketStream.API.Controllers;

/// <summary>Image uploads for team logos, player cut-outs, stadium backgrounds and banners.
/// The type is decided from the file's first bytes, never from its name or declared content type; SVG is refused.</summary>
[Route("api/media"), Authorize(Roles = Roles.Admin)]
public class MediaController : ApiControllerBase {
    private const long MaxBytes = 5 * 1024 * 1024;
    private readonly UploadOptions _options;
    public MediaController(UploadOptions options) { _options = options; }

    private static string? Sniff(byte[] h) {
        if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return ".png";
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (h.Length >= 12 && h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46 && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50) return ".webp";
        if (h.Length >= 6 && h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38) return ".gif";
        return null;
    }

    [HttpPost, RequestSizeLimit(MaxBytes + 4096)]
    public async Task<IActionResult> Upload(IFormFile file) {
        if (file is null || file.Length == 0) return Problem(detail: "No file was uploaded.", statusCode: 400);
        if (file.Length > MaxBytes) return Problem(detail: "Images can be at most 5 MB.", statusCode: 400);
        var header = new byte[12];
        await using (var s = file.OpenReadStream()) { var read = await s.ReadAsync(header.AsMemory(0, 12)); Array.Resize(ref header, read); }
        var ext = Sniff(header);
        if (ext is null) return Problem(detail: "Only PNG, JPEG, WebP or GIF images are accepted.", statusCode: 400);
        var name = $"{Guid.NewGuid():N}{ext}";
        await using (var target = System.IO.File.Create(Path.Combine(_options.Path, name))) await file.CopyToAsync(target);
        return Ok(new { url = $"/uploads/{name}" });
    }
}

