namespace CvTailr.Api.Services;

/// <summary>
/// Thrown by <see cref="Interfaces.IJdSourceResolver"/> when a submitted job posting URL
/// can't be fetched. The message is safe to surface directly to the end user.
/// </summary>
public class JdSourceResolutionException(string message, Exception? innerException = null)
    : Exception(message, innerException);
