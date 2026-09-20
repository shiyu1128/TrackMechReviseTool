using System.Globalization;
using System.Text.RegularExpressions;

namespace TrackMechReviseTool.Core;

public sealed class OutMechanismParser
{
    private static readonly Regex ElementRowPattern = new(
        @"^\s*(\d+)\.\s+([A-Z][A-Z0-9]*)\s+([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:E[-+]?\d+)?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SpeciesRowPattern = new(
        @"^\s*(\d+)\.\s+(\S+)\s+(\S+)\s+([-+]?\d+)\s+([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:E[-+]?\d+)?)\s+([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:E[-+]?\d+)?)\s+([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:E[-+]?\d+)?)(.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ReactionRowPattern = new(
        @"^\s*(\d+)\.\s+(\S+)\s+([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:E[-+]?\d+)?)\s+([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:E[-+]?\d+)?)\s+([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:E[-+]?\d+)?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EnhancedByPattern = new(
        @"^\s*(\S+)\s+Enhanced by\s+([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:E[-+]?\d+)?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public OutMechanism Parse(string path)
    {
        var lines = File.ReadAllLines(path);
        var mechanism = new OutMechanism();

        ParseElements(lines, mechanism);
        ParseSpecies(lines, mechanism);
        ParseReactions(lines, mechanism);

        return mechanism;
    }

    private static void ParseElements(IReadOnlyList<string> lines, OutMechanism mechanism)
    {
        var inElementBlock = false;

        foreach (var line in lines)
        {
            if (line.Contains("ELEMENTS", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("ATOMIC", StringComparison.OrdinalIgnoreCase))
            {
                inElementBlock = true;
                continue;
            }

            if (!inElementBlock)
            {
                continue;
            }

            if (line.Contains("WARNING...", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var match = ElementRowPattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            mechanism.Elements.Add(new ChemicalElement(
                match.Groups[2].Value,
                ParseDouble(match.Groups[3].Value)));
        }
    }

    private static void ParseSpecies(IReadOnlyList<string> lines, OutMechanism mechanism)
    {
        List<string>? elementColumns = null;
        var inSpeciesBlock = false;

        foreach (var line in lines)
        {
            if (line.Contains("WEIGHT", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("LOW", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("HIGH", StringComparison.OrdinalIgnoreCase))
            {
                elementColumns = ExtractElementColumns(line);
                inSpeciesBlock = true;
                continue;
            }

            if (!inSpeciesBlock || elementColumns is null)
            {
                continue;
            }

            if (line.Contains("REACTIONS CONSIDERED", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var match = SpeciesRowPattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var counts = ParseElementCounts(match.Groups[8].Value, elementColumns);
            mechanism.Species.Add(new SpeciesInfo(
                int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                match.Groups[2].Value,
                match.Groups[3].Value,
                int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture),
                ParseDouble(match.Groups[5].Value),
                ParseDouble(match.Groups[6].Value),
                ParseDouble(match.Groups[7].Value),
                counts));
        }
    }

    private static void ParseReactions(IReadOnlyList<string> lines, OutMechanism mechanism)
    {
        var inReactionBlock = false;
        ReactionInfo? current = null;

        foreach (var line in lines)
        {
            if (line.Contains("REACTIONS CONSIDERED", StringComparison.OrdinalIgnoreCase))
            {
                inReactionBlock = true;
                continue;
            }

            if (!inReactionBlock)
            {
                continue;
            }

            var reactionMatch = ReactionRowPattern.Match(line);
            if (reactionMatch.Success)
            {
                current = new ReactionInfo
                {
                    Index = int.Parse(reactionMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                    Equation = reactionMatch.Groups[2].Value,
                    Rate = new ArrheniusRate(
                        ParseDouble(reactionMatch.Groups[3].Value),
                        ParseDouble(reactionMatch.Groups[4].Value),
                        ParseDouble(reactionMatch.Groups[5].Value))
                };

                mechanism.Reactions.Add(current);
                continue;
            }

            if (current is null)
            {
                continue;
            }

            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("Declared duplicate reaction", StringComparison.OrdinalIgnoreCase))
            {
                current.IsDuplicate = true;
                continue;
            }

            if (trimmed.StartsWith("Warning", StringComparison.OrdinalIgnoreCase))
            {
                current.Warnings.Add(trimmed);
                continue;
            }

            if (trimmed.StartsWith("Low pressure limit:", StringComparison.OrdinalIgnoreCase))
            {
                current.LowPressureLimit.AddRange(ParseNumbersAfterColon(trimmed));
                continue;
            }

            if (trimmed.StartsWith("TROE centering:", StringComparison.OrdinalIgnoreCase))
            {
                current.TroeCentering.AddRange(ParseNumbersAfterColon(trimmed));
                continue;
            }

            var enhancedMatch = EnhancedByPattern.Match(line);
            if (enhancedMatch.Success)
            {
                current.ColliderEfficiencies[enhancedMatch.Groups[1].Value] = ParseDouble(enhancedMatch.Groups[2].Value);
                continue;
            }

            current.Notes.Add(trimmed);
        }
    }

    private static List<string> ExtractElementColumns(string line)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var highIndex = Array.FindIndex(tokens, token => string.Equals(token, "HIGH", StringComparison.OrdinalIgnoreCase));
        return highIndex < 0 || highIndex + 1 >= tokens.Length
            ? []
            : tokens.Skip(highIndex + 1).ToList();
    }

    private static IReadOnlyDictionary<string, int> ParseElementCounts(string text, IReadOnlyList<string> elementColumns)
    {
        var countTokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < elementColumns.Count && i < countTokens.Length; i++)
        {
            counts[elementColumns[i]] = int.Parse(countTokens[i], CultureInfo.InvariantCulture);
        }

        return counts;
    }

    private static IEnumerable<double> ParseNumbersAfterColon(string text)
    {
        var colonIndex = text.IndexOf(':');
        var numberText = colonIndex >= 0 ? text[(colonIndex + 1)..] : text;
        return numberText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseDouble)
            .ToList();
    }

    private static double ParseDouble(string value)
    {
        return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
