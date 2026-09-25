namespace CvTailr.Api.Exceptions;

/// <summary>
/// Thrown by <see cref="CvTailr.Api.Services.Interfaces.ICvSourceExtractor"/> when a CV file's
/// extension isn't supported, or when no readable text could be extracted from it. The message
/// is safe to surface directly to the end user.
/// </summary>
public class UnsupportedCvFormatException(string message) : Exception(message);
