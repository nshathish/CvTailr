using CvTailr.Jd.Api.Infrastructure.Foundry;
using CvTailr.Shared.Jd;
using Microsoft.Extensions.Options;

namespace CvTailr.Jd.Api.Infrastructure;

public class JdParsingService(
    IFoundryClient foundryClient,
    IOptions<FoundryOptions> foundryOptions) : IJdParsingService
{
    private const string SystemPrompt =
        """
        You are an expert technical recruiter. Extract structured requirements from the job
        description provided by the user.

        Return a JSON object matching exactly this shape:
        {
          "RoleTitle": string | null,
          "CompanyName": string | null,
          "CompanyDomain": string | null,
          "Requirements": [
            { "Skill": string, "Priority": "MustHave" | "NiceToHave", "YearsRequired": string | null, "Notes": string | null }
          ],
          "EmphasizedLanguages": string[]
        }

        Rules:
        - "EmphasizedLanguages" may ONLY contain values drawn from this exact set: "Python", "C#",
          "TypeScript", "Java". Include a language only if the job description clearly emphasizes
          it as a requirement for the role. Never include any language outside this set. If none
          of these four languages are emphasized, return an empty array.
        - "Priority" must be exactly "MustHave" or "NiceToHave" for every requirement.
        - Do not invent requirements that aren't supported by the text.
        - "RoleTitle" is the job title as stated in the text. If it genuinely cannot be determined,
          return null. Never return a placeholder like "Unknown" or an invented title.
        - "CompanyName" is the hiring company's name as stated in the text. If it genuinely cannot
          be determined (e.g. a recruiter posting with no company disclosed), return null. Never
          return a placeholder like "Unknown" or an invented name.
        - "CompanyDomain" is the hiring company's own website domain (e.g. "monzo.com"). Return it
          ONLY when the text explicitly contains the company's own website URL or an email address
          at the company's own domain. Otherwise return null. Never infer a domain from the
          company name alone.
        """;

    private readonly FoundryOptions _foundryOptions = foundryOptions.Value;

    public async Task<JdRequirements> ParseAsync(string rawJdText, CancellationToken cancellationToken = default)
    {
        var result = await foundryClient.GetStructuredCompletionAsync<JdRequirements>(
            SystemPrompt,
            rawJdText,
            _foundryOptions.JdParsingDeploymentName,
            cancellationToken);

        result.RawJdText = rawJdText;
        return result;
    }
}
