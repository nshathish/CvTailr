using CvTailr.Jd.Api.Infrastructure;

namespace CvTailr.Jd.Api.Domain;

/// <summary>
/// Thrown by <see cref="IJdSourceResolver"/> and
/// <see cref="ErrorCode"/> when a submitted job posting
/// URL can't be turned into JD text. <see cref="JdUrlFetcher"/> is one of "UrlNotAllowed",
/// "UrlFetchFailed", or "UrlNotReadable". The message is safe to surface directly to the end user.
/// </summary>
public class JdUrlException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
