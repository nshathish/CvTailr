using CvTailr.Api.Data;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Enums;

namespace CvTailr.Api.Services;

public class CvParsingService(ICvRepository cvRepository) : ICvParsingService
{
    public async Task<CvDocument> UploadAndParseAsync(string userId, string rawLatexSource, CancellationToken cancellationToken = default)
    {
        var document = BuildStubDocument(rawLatexSource);
        document.UserId = userId;

        await cvRepository.UpsertAsync(document, cancellationToken);

        return document;
    }

    public Task<CvDocument?> GetCurrentAsync(string userId, CancellationToken cancellationToken = default) =>
        cvRepository.GetByUserIdAsync(userId, cancellationToken);

    // TODO: this is a stub. Replace with a real call to latex-service via a
    // future Clients/LatexServiceClient.cs once that client exists — rawLatexSource
    // is currently ignored and a hardcoded CvDocument is returned instead.
    private static CvDocument BuildStubDocument(string rawLatexSource)
    {
        var document = new CvDocument
        {
            RawLatexSource = rawLatexSource,
            Roles =
            [
                new CvRole
                {
                    CompanyName = "Zensar Technologies",
                    Title = "Principal Engineer",
                    StartDate = new DateOnly(2025, 5, 1),
                    EndDate = null,
                    Bullets =
                    [
                        new CvBullet
                        {
                            OriginalText = "Designed customer-facing portals enabling clients to monitor and manage industrial reactor systems through intuitive, data-driven interfaces.",
                            Language = null
                        },
                        new CvBullet
                        {
                            OriginalText = "Built stateful Azure Durable Functions with .NET Core to orchestrate long-running client data workflows and maintain state consistency across distributed systems.",
                            Language = "C#"
                        },
                        new CvBullet
                        {
                            OriginalText = "Built real-time data ingestion pipelines using Azure and event-driven .NET services to capture and process reactor sensor telemetry.",
                            Language = "C#"
                        },
                        new CvBullet
                        {
                            OriginalText = "Designed scalable, secure ASP.NET Core Web APIs on Azure App Services applying clean architecture and CQRS to separate read/write concerns across microservices.",
                            Language = "C#"
                        },
                        new CvBullet
                        {
                            OriginalText = "Configured Azure API Management to centralise gateway policies, throttling and versioning across .NET Core microservices.",
                            Language = "C#"
                        },
                        new CvBullet
                        {
                            OriginalText = "Implemented observability with Azure Monitor and Application Insights for proactive performance tuning and rapid incident diagnosis.",
                            Language = null
                        },
                        new CvBullet
                        {
                            OriginalText = "Managed secrets and certificates through Azure Key Vault, integrated into .NET Core configuration providers.",
                            Language = "C#"
                        },
                        new CvBullet
                        {
                            OriginalText = "Established infrastructure-as-code with Terraform and GitHub Actions to automate and standardise Azure deployment pipelines.",
                            Language = null
                        },
                        new CvBullet
                        {
                            OriginalText = "Built responsive portal applications in Next.js using SSR/static generation, and multi-language (English/Chinese) support on a single codebase.",
                            Language = "TypeScript"
                        },
                        new CvBullet
                        {
                            OriginalText = "Delivered a feature management system for controlled rollouts and A/B testing, and secure RBAC using Azure Entra.",
                            Language = null
                        }
                    ]
                }
            ],
            Skills =
            [
                new CvSkill { Name = "Next.js", Proficiency = ProficiencyLevel.Unknown, IsProvisional = false },
                new CvSkill { Name = ".NET 8", Proficiency = ProficiencyLevel.Unknown, IsProvisional = false },
                new CvSkill { Name = "ASP.NET Web API", Proficiency = ProficiencyLevel.Unknown, IsProvisional = false },
                new CvSkill { Name = "Azure Functions/Durable Functions", Proficiency = ProficiencyLevel.Unknown, IsProvisional = false },
                new CvSkill { Name = "Service Bus", Proficiency = ProficiencyLevel.Unknown, IsProvisional = false },
                new CvSkill { Name = "API Management", Proficiency = ProficiencyLevel.Unknown, IsProvisional = false },
                new CvSkill { Name = "App Services", Proficiency = ProficiencyLevel.Unknown, IsProvisional = false },
                new CvSkill { Name = "Key Vault", Proficiency = ProficiencyLevel.Unknown, IsProvisional = false },
                new CvSkill { Name = "Monitor/Application Insights", Proficiency = ProficiencyLevel.Unknown, IsProvisional = false },
                new CvSkill { Name = "Azure Entra", Proficiency = ProficiencyLevel.Unknown, IsProvisional = false },
                new CvSkill { Name = "Terraform", Proficiency = ProficiencyLevel.Unknown, IsProvisional = false },
                new CvSkill { Name = "GitHub Actions", Proficiency = ProficiencyLevel.Unknown, IsProvisional = false },
                new CvSkill { Name = "TypeScript", Proficiency = ProficiencyLevel.Unknown, IsProvisional = false },
                new CvSkill { Name = "C#", Proficiency = ProficiencyLevel.Unknown, IsProvisional = false }
            ]
        };

        return document;
    }
}
