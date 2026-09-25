using System.Text;
using CvTailr.Api.Exceptions;
using CvTailr.Api.Services.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace CvTailr.Api.Services;

public class CvSourceExtractor : ICvSourceExtractor
{
    private static readonly string[] SupportedExtensions = [".txt", ".tex", ".pdf", ".docx"];

    public async Task<string> ExtractAsync(Stream fileStream, string fileName, CancellationToken ct)
    {
        var extension = Path.GetExtension(fileName);

        var text = extension.ToLowerInvariant() switch
        {
            ".txt" or ".tex" => await ExtractPlainTextAsync(fileStream, ct),
            ".pdf" => ExtractPdfText(fileStream),
            ".docx" => ExtractDocxText(fileStream),
            _ => throw new UnsupportedCvFormatException(
                $"Unsupported CV file type '{extension}'. Supported types: {string.Join(", ", SupportedExtensions)}.")
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new UnsupportedCvFormatException(
                "Couldn't extract readable text from this file — it may be a scanned image. Try pasting the text directly.");
        }

        return text;
    }

    private static async Task<string> ExtractPlainTextAsync(Stream fileStream, CancellationToken ct)
    {
        using var reader = new StreamReader(fileStream, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }

    private static string ExtractPdfText(Stream fileStream)
    {
        using var document = PdfDocument.Open(fileStream);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }

    private static string ExtractDocxText(Stream fileStream)
    {
        using var document = WordprocessingDocument.Open(fileStream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        return body?.InnerText ?? string.Empty;
    }
}
