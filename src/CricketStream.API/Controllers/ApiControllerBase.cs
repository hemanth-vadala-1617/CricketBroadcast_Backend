using System.Security.Claims;
using CricketStream.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace CricketStream.API.Controllers;

public static class Roles {
    public const string Admin = "SuperAdmin,Admin";
    public const string Scoring = "SuperAdmin,Admin,Scorer";
    public const string Graphics = "SuperAdmin,Admin,Producer,Scorer";
    public const string AnyStaff = "SuperAdmin,Admin,Scorer,Producer";
}

[ApiController]
public abstract class ApiControllerBase : ControllerBase {
    /// <summary>Always taken from the JWT, never from a request body.</summary>
    protected string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    protected IActionResult Problem(Error e) {
        var status = e.Kind switch {
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Unprocessable => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest,
        };
        return Problem(detail: e.Message, statusCode: status, title: e.Kind.ToString());
    }

    protected IActionResult From<T>(Result<T> r) => r.Succeeded ? Ok(r.Value) : Problem(r.Error!);
    protected IActionResult Created<T>(Result<T> r, string action, Func<T, object> route) =>
        r.Succeeded ? CreatedAtAction(action, route(r.Value!), r.Value) : Problem(r.Error!);
}
