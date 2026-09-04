using CvTailr.Api.Services.Interfaces;

namespace CvTailr.Api.Services;

public class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    // Entra sometimes maps the short "oid" claim type to this long URI form depending on
    // JwtBearerOptions.MapInboundClaims — check both so behavior doesn't depend on that setting.
    private const string ObjectIdClaimType = "oid";
    private const string ObjectIdClaimTypeUri = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    public string GetUserId()
    {
        var user = httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("No HttpContext is available to resolve the current user.");

        var objectId = user.FindFirst(ObjectIdClaimType)?.Value
            ?? user.FindFirst(ObjectIdClaimTypeUri)?.Value;

        return string.IsNullOrWhiteSpace(objectId)
            ? throw new InvalidOperationException("The authenticated user's token has no 'oid' claim.")
            : objectId;
    }
}
