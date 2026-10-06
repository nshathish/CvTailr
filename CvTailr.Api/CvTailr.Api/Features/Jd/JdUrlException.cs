namespace CvTailr.Api.Features.Jd;

/// <summary>
/// Thrown by <see cref="Jd.JdUrlFetcher"/> and
/// <see cref="IJdSourceResolver"/> when a submitted job posting
/// URL can't be turned into JD text. <see cref="ErrorCode"/> is one of "UrlNotAllowed",
/// "UrlFetchFailed", or "UrlNotReadable". The message is safe to surface directly to the end user.
/// </summary>
public class JdUrlException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
