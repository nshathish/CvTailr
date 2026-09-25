using System.Text.Json;
using System.Text.Json.Serialization;
using CvTailr.Shared.Jobs;

namespace CvTailr.Api.Data.Converters;

/// <summary>
/// (De)serializes JobStatus as a string for Cosmos storage, and maps values from the retired
/// Draft/Scored/Tailored scheme (Draft = 0, Scored = 1, Tailored = 2, stored as ints under the
/// default serializer) onto the closest of the current three values: Draft -> Scored (the earliest
/// current stage), Scored -> Scored, Tailored -> Tailored. Unrecognized/malformed values also fall
/// back to Scored rather than throwing, since a Job that made it into storage at all has at least
/// reached that stage.
/// </summary>
public class LegacyJobStatusJsonConverter : JsonConverter<JobStatus>
{
    public override JobStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var legacyOrdinal))
        {
            return legacyOrdinal switch
            {
                2 => JobStatus.Tailored,
                _ => JobStatus.Scored
            };
        }

        var text = reader.GetString();
        return Enum.TryParse<JobStatus>(text, ignoreCase: true, out var status)
            ? status
            : JobStatus.Scored;
    }

    public override void Write(Utf8JsonWriter writer, JobStatus value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
