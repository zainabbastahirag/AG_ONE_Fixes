using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using NexaEmailBlast.Models;

namespace NexaEmailBlast.Services;

/// <summary>
/// Reads recipients from a CSV file. Expected headers (case-insensitive): Email, Name.
/// Extra columns are ignored. If the file is missing or has no valid rows the caller
/// falls back to the configured default recipient.
/// </summary>
public static class CsvRecipientReader
{
    public static List<Recipient> Read(string csvPath)
    {
        var result = new List<Recipient>();
        if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
            return result;

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = true,
        };

        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, config);

        if (!csv.Read() || !csv.ReadHeader())
            return result;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (csv.Read())
        {
            var email = (csv.TryGetField<string>("email", out var e) ? e : null)?.Trim();
            var name = (csv.TryGetField<string>("name", out var n) ? n : null)?.Trim();

            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                continue;
            if (!seen.Add(email))
                continue;

            result.Add(new Recipient
            {
                Email = email,
                Name = string.IsNullOrWhiteSpace(name) ? null : name,
            });
        }

        return result;
    }
}
