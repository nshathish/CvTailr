using System.Globalization;
using CvTailr.Api.Clients;
using CvTailr.Api.Configuration;
using CvTailr.Api.Data.Interfaces;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Enums;
using Microsoft.Extensions.Options;

namespace CvTailr.Api.Services;

public class CvParsingService(
    ICvRepository cvRepository,
    IFoundryClient foundryClient,
    IOptions<FoundryOptions> foundryOptions,
    ILogger<CvParsingService> logger) : ICvParsingService
{
    // The only four languages the language-substitution rule may ever touch (root CLAUDE.md rule 2).
    private static readonly HashSet<string> AllowedLanguages = ["Python", "C#", "TypeScript", "Java"];

    private const string SystemPrompt =
        """
        You are an expert CV parser. You will be given the raw LaTeX source of a
        candidate's CV. Extract its content into structured data.

        Return a JSON object matching exactly this shape:
        {
          "Roles": [
            {
              "CompanyName": string,
              "Title": string,
              "StartDate": string,
              "EndDate": string | null,
              "Bullets": [
                { "OriginalText": string, "Language": string | null }
              ]
            }
          ],
          "Skills": [ { "Name": string } ]
        }

        Rules:
        - One entry in "Roles" per role/position found in the document.
        - "StartDate" and "EndDate" must be ISO "yyyy-MM-dd" with the day defaulted to
          "01" (CVs typically only give month/year). Set "EndDate" to null if the role
          is current/ongoing (phrases like "Present", "Current", "Now").
        - CRITICAL: preserve "CompanyName", "StartDate", and "EndDate" EXACTLY as
          written in the source (aside from the required ISO date reformatting) — never
          reword, reformat further, or "clean up" a company name or date.
        - "Bullets" text must be clean, readable plain text with all LaTeX
          commands/markup stripped — never return raw LaTeX syntax.
        - For each bullet, set "Language" to one of exactly "Python", "C#",
          "TypeScript", "Java" ONLY if the bullet clearly and unambiguously references
          that language; otherwise null. Never guess a language from surrounding
          context.
        - "Skills" is a flat list gathered from wherever the source lists them (a
          dedicated skills section, "Technology/Tools" lines, etc.) — name only.
        - If the source contains no discernible roles, return an empty "Roles" array
          rather than inventing content.
        """;

    private readonly FoundryOptions _foundryOptions = foundryOptions.Value;

    public async Task<CvDocument> UploadAndParseAsync(string userId, string rawLatexSource,
        CancellationToken cancellationToken = default)
    {
        var document = await ExtractDocumentFromLatexAsync(rawLatexSource, cancellationToken);
        document.UserId = userId;
        document.RawLatexSource = rawLatexSource;

        await cvRepository.UpsertAsync(document, cancellationToken);

        return document;
    }

    public Task<CvDocument?> GetCurrentAsync(string userId, CancellationToken cancellationToken = default) =>
        cvRepository.GetByUserIdAsync(userId, cancellationToken);

    private async Task<CvDocument> ExtractDocumentFromLatexAsync(string rawLatexSource,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawLatexSource))
            throw new ArgumentException("rawLatexSource is required.", nameof(rawLatexSource));

        var completion = await foundryClient.GetStructuredCompletionAsync<CvExtractionCompletion>(
            SystemPrompt,
            rawLatexSource,
            _foundryOptions.JdParsingDeploymentName,
            cancellationToken);

        return ValidateAndSanitize(completion);
    }

    private CvDocument ValidateAndSanitize(CvExtractionCompletion completion)
    {
        var document = new CvDocument();

        foreach (var roleCandidate in completion.Roles)
        {
            if (string.IsNullOrWhiteSpace(roleCandidate.CompanyName) || string.IsNullOrWhiteSpace(roleCandidate.Title))
            {
                logger.LogWarning(
                    "Excluding extracted role with missing CompanyName/Title. CompanyName={CompanyName} Title={Title}",
                    roleCandidate.CompanyName, roleCandidate.Title);
                continue;
            }

            if (!TryParseDate(roleCandidate.StartDate, out var startDate))
            {
                logger.LogWarning(
                    "Excluding extracted role '{CompanyName}' / '{Title}': unparseable StartDate '{StartDate}'.",
                    roleCandidate.CompanyName, roleCandidate.Title, roleCandidate.StartDate);
                continue;
            }

            DateOnly? endDate = null;
            if (!string.IsNullOrWhiteSpace(roleCandidate.EndDate))
            {
                if (TryParseDate(roleCandidate.EndDate, out var parsedEndDate))
                {
                    endDate = parsedEndDate;
                }
                else
                {
                    logger.LogWarning(
                        "Ignoring unparseable EndDate '{EndDate}' for role '{CompanyName}' / '{Title}'; treating the role as ongoing.",
                        roleCandidate.EndDate, roleCandidate.CompanyName, roleCandidate.Title);
                }
            }

            document.Roles.Add(new CvRole
            {
                CompanyName = roleCandidate.CompanyName.Trim(),
                Title = roleCandidate.Title.Trim(),
                StartDate = startDate,
                EndDate = endDate,
                Bullets = roleCandidate.Bullets
                    .Where(bullet => !string.IsNullOrWhiteSpace(bullet.OriginalText))
                    .Select(bullet => new CvBullet
                    {
                        OriginalText = bullet.OriginalText.Trim(),
                        Language = SanitizeLanguage(bullet.Language)
                    })
                    .ToList()
            });
        }

        document.Skills = completion.Skills
            .Where(skill => !string.IsNullOrWhiteSpace(skill.Name))
            .Select(skill => new CvSkill
            {
                Name = skill.Name.Trim(),
                Proficiency = ProficiencyLevel.Unknown,
                IsProvisional = false
            })
            .ToList();

        return document;
    }

    private string? SanitizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        if (AllowedLanguages.Contains(language))
            return language;

        logger.LogWarning("Discarding out-of-scope extracted bullet language '{Language}'.", language);
        return null;
    }

    private static bool TryParseDate(string? value, out DateOnly result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;

        if (DateOnly.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var yearMonth))
        {
            result = new DateOnly(yearMonth.Year, yearMonth.Month, 1);
            return true;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    protected sealed class CvExtractionCompletion
    {
        public List<RoleCandidate> Roles { get; init; } = new();
        public List<SkillCandidate> Skills { get; init; } = new();
    }

    protected sealed class RoleCandidate
    {
        public string CompanyName { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? StartDate { get; init; }
        public string? EndDate { get; init; }
        public List<BulletCandidate> Bullets { get; init; } = new();
    }

    protected sealed class BulletCandidate
    {
        public string OriginalText { get; init; } = string.Empty;
        public string? Language { get; init; }
    }

    protected sealed class SkillCandidate
    {
        public string Name { get; init; } = string.Empty;
    }
}