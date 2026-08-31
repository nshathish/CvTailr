using CvTailr.Api.Services.Interfaces;

namespace CvTailr.Api.Services;

// TODO: replace with real Entra ID user resolution — GetUserId should read the JWT claim
// (e.g. "oid" or "sub") once auth is wired up, instead of returning a constant. This is the
// ONLY place in the codebase that should know about the placeholder dev user ID.
public class CurrentUserContext : ICurrentUserContext
{
    private const string DevUserId = "dev-user-001";

    public string GetUserId() => DevUserId;
}
