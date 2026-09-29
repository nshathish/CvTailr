namespace CvTailr.Api.Exceptions;

/// <summary>
/// Thrown by <see cref="CvTailr.Api.Services.Interfaces.IJdUrlFetcher"/> and
/// <see cref="CvTailr.Api.Services.Interfaces.IJdSourceResolver"/> when a submitted job posting
/// URL can't be turned into JD text. <see cref="ErrorCode"/> is one of "UrlNotAllowed",
/// "UrlFetchFailed", or "UrlNotReadable". The message is safe to surface directly to the end user.
/// </summary>
public class JdUrlException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
