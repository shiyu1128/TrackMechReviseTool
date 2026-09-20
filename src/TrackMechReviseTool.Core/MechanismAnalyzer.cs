namespace TrackMechReviseTool.Core;

public sealed class MechanismAnalyzer
{
    public IReadOnlyList<SpeciesInfo> SpeciesContainingElement(OutMechanism mechanism, string elementSymbol)
    {
        return mechanism.Species
            .Where(species => species.ContainsElement(elementSymbol))
            .OrderBy(species => species.Index)
            .ToList();
    }

    public IReadOnlyList<ReactionInfo> ReactionsMentioningSpecies(
        OutMechanism mechanism,
        IEnumerable<SpeciesInfo> species)
    {
        var names = species
            .Select(item => item.Name)
            .OrderByDescending(name => name.Length)
            .ToArray();

        return mechanism.Reactions
            .Where(reaction => names.Any(name => ReactionMentionsSpecies(reaction.Equation, name)))
            .OrderBy(reaction => reaction.Index)
            .ToList();
    }

    private static bool ReactionMentionsSpecies(string equation, string speciesName)
    {
        var normalizedEquation = RemoveThirdBodyDecorations(equation);
        var tokens = normalizedEquation
            .Replace("<=>", "+", StringComparison.Ordinal)
            .Replace("=>", "+", StringComparison.Ordinal)
            .Replace("=", "+", StringComparison.Ordinal)
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(RemoveLeadingStoichiometricCoefficient);

        return tokens.Any(token => string.Equals(token, speciesName, StringComparison.OrdinalIgnoreCase));
    }

    private static string RemoveThirdBodyDecorations(string token)
    {
        return token.Replace("(+M)", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveLeadingStoichiometricCoefficient(string token)
    {
        var index = 0;
        while (index < token.Length && char.IsDigit(token[index]))
        {
            index++;
        }

        return index == 0 ? token : token[index..];
    }
}
