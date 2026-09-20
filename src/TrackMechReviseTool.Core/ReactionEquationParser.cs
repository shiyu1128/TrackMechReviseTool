using System.Text.RegularExpressions;

namespace TrackMechReviseTool.Core;

public enum ReactionArrowKind
{
    Reversible,
    Irreversible
}

public sealed record ReactionParticipant(string SpeciesName, int StoichiometricCoefficient);

public sealed record ParsedReactionEquation(
    IReadOnlyList<ReactionParticipant> Reactants,
    IReadOnlyList<ReactionParticipant> Products,
    ReactionArrowKind ArrowKind,
    string ArrowText);

public sealed class ReactionEquationParser
{
    private static readonly Regex ArrowPattern = new(
        @"<=>|=>|=",
        RegexOptions.Compiled);

    private static readonly Regex CoefficientPattern = new(
        @"^(?<coefficient>\d+)(?<species>\D.*)$",
        RegexOptions.Compiled);

    public ParsedReactionEquation Parse(string equation)
    {
        var arrow = ArrowPattern.Match(equation);
        if (!arrow.Success)
        {
            throw new FormatException($"Reaction equation has no supported arrow: {equation}");
        }

        var reactants = ParseSide(equation[..arrow.Index]);
        var products = ParseSide(equation[(arrow.Index + arrow.Length)..]);
        var arrowKind = string.Equals(arrow.Value, "=>", StringComparison.Ordinal)
            ? ReactionArrowKind.Irreversible
            : ReactionArrowKind.Reversible;

        return new ParsedReactionEquation(reactants, products, arrowKind, arrow.Value);
    }

    private static IReadOnlyList<ReactionParticipant> ParseSide(string side)
    {
        var normalized = side.Replace("(+M)", string.Empty, StringComparison.OrdinalIgnoreCase);
        var participants = new List<ReactionParticipant>();

        foreach (var rawToken in normalized.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(rawToken, "M", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var coefficientMatch = CoefficientPattern.Match(rawToken);
            var coefficient = coefficientMatch.Success
                ? int.Parse(coefficientMatch.Groups["coefficient"].Value, System.Globalization.CultureInfo.InvariantCulture)
                : 1;
            var species = coefficientMatch.Success
                ? coefficientMatch.Groups["species"].Value
                : rawToken;

            participants.Add(new ReactionParticipant(species, coefficient));
        }

        return participants;
    }
}
