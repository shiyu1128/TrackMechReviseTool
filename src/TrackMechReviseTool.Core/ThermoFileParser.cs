using System.Text;

namespace TrackMechReviseTool.Core;

public sealed record ThermoEntry(string SpeciesName, IReadOnlyList<string> Lines);

public sealed record ThermoDocument(
    IReadOnlyList<string> Lines,
    int EndLineIndex,
    IReadOnlyDictionary<string, ThermoEntry> Entries);

public sealed class ThermoFileParser
{
    private const int RecordNumberColumn = 79;
    private const int SpeciesFieldWidth = 18;

    public ThermoDocument Parse(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var thermoLineIndex = Array.FindIndex(lines, line =>
            string.Equals(line.Trim(), "THERMO", StringComparison.OrdinalIgnoreCase));
        if (thermoLineIndex < 0)
        {
            throw new FormatException("Thermodynamic file has no THERMO section.");
        }

        var endLineIndex = Array.FindIndex(lines, thermoLineIndex + 1, line =>
            string.Equals(line.Trim(), "END", StringComparison.OrdinalIgnoreCase));
        if (endLineIndex < 0)
        {
            throw new FormatException("Thermodynamic file has no END marker.");
        }

        var entries = new Dictionary<string, ThermoEntry>(StringComparer.OrdinalIgnoreCase);
        for (var index = thermoLineIndex + 1; index + 3 < endLineIndex; index++)
        {
            if (!IsRecordLine(lines[index], '1') ||
                !IsRecordLine(lines[index + 1], '2') ||
                !IsRecordLine(lines[index + 2], '3') ||
                !IsRecordLine(lines[index + 3], '4') ||
                IsComment(lines[index]))
            {
                continue;
            }

            var speciesName = ParseSpeciesName(lines[index]);
            if (speciesName.Length == 0 || speciesName.Contains('@'))
            {
                continue;
            }
            if (!entries.TryAdd(
                    speciesName,
                    new ThermoEntry(speciesName, lines.Skip(index).Take(4).ToArray())))
            {
                throw new FormatException($"Thermodynamic file contains duplicate active entries for '{speciesName}'.");
            }

            index += 3;
        }

        if (entries.Count == 0)
        {
            throw new FormatException("Thermodynamic file contains no active four-line NASA entries.");
        }

        return new ThermoDocument(lines, endLineIndex, entries);
    }

    private static bool IsRecordLine(string line, char recordNumber)
    {
        return line.Length > RecordNumberColumn && line[RecordNumberColumn] == recordNumber;
    }

    private static bool IsComment(string line)
    {
        return line.TrimStart().StartsWith('!');
    }

    private static string ParseSpeciesName(string line)
    {
        var speciesField = line[..Math.Min(SpeciesFieldWidth, line.Length)].Trim();
        return speciesField.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
    }
}
