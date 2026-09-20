namespace TrackMechReviseTool.Core;

public sealed record LabeledSideState(
    string EquationSide,
    int MarkedAtomCount,
    int AssignmentMultiplicity);

public sealed record LabeledReactionCandidate(
    int MarkedAtomCount,
    string Equation,
    string ReactantState,
    string ProductState,
    int ForwardAssignmentMultiplicity,
    int ReverseAssignmentMultiplicity);

public sealed record LabelCountLayer(
    int MarkedAtomCount,
    int ReactantStateCount,
    int ProductStateCount,
    int CandidateReactionCount);

public sealed record LabeledReactionExpansion(
    int TotalElementAtomCount,
    ReactionArrowKind ArrowKind,
    bool IsComplete,
    IReadOnlyList<string> MissingSpeciesRules,
    IReadOnlyList<LabelCountLayer> Layers,
    IReadOnlyList<LabeledReactionCandidate> Candidates)
{
    public int AddedReactionTypeCount => Candidates.Count;
}

public sealed class LabeledReactionTypeGenerator
{
    private readonly ReactionEquationParser equationParser = new();

    public LabeledReactionExpansion Generate(
        OutMechanism mechanism,
        ReactionInfo reaction,
        string elementSymbol,
        IReadOnlyDictionary<string, SpeciesTraceRule> ruleMap)
    {
        var equation = equationParser.Parse(reaction.Equation);
        var speciesByName = mechanism.Species.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var missingRules = equation.Reactants
            .Concat(equation.Products)
            .Where(item => ContainsElement(item.SpeciesName, speciesByName, elementSymbol))
            .Where(item => !ruleMap.ContainsKey(item.SpeciesName))
            .Select(item => item.SpeciesName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToArray();

        var reactantStates = ExpandSide(equation.Reactants, ruleMap);
        var productStates = ExpandSide(equation.Products, ruleMap);
        var totalAtoms = CountElementAtoms(equation.Reactants, speciesByName, elementSymbol);
        var candidates = new List<LabeledReactionCandidate>();

        for (var markedAtoms = 1; markedAtoms <= totalAtoms; markedAtoms++)
        {
            var matchingReactants = reactantStates.Where(item => item.MarkedAtomCount == markedAtoms).ToArray();
            var matchingProducts = productStates.Where(item => item.MarkedAtomCount == markedAtoms).ToArray();

            foreach (var reactantState in matchingReactants)
            {
                foreach (var productState in matchingProducts)
                {
                    candidates.Add(new LabeledReactionCandidate(
                        markedAtoms,
                        FormatEquation(reaction.Equation, equation, reactantState.EquationSide, productState.EquationSide),
                        reactantState.EquationSide,
                        productState.EquationSide,
                        reactantState.AssignmentMultiplicity,
                        productState.AssignmentMultiplicity));
                }
            }
        }

        var layers = Enumerable.Range(1, totalAtoms)
            .Select(markedAtoms => new LabelCountLayer(
                markedAtoms,
                reactantStates.Count(item => item.MarkedAtomCount == markedAtoms),
                productStates.Count(item => item.MarkedAtomCount == markedAtoms),
                candidates.Count(item => item.MarkedAtomCount == markedAtoms)))
            .ToArray();

        return new LabeledReactionExpansion(
            totalAtoms,
            equation.ArrowKind,
            missingRules.Length == 0,
            missingRules,
            layers,
            candidates);
    }

    private static IReadOnlyList<LabeledSideState> ExpandSide(
        IReadOnlyList<ReactionParticipant> participants,
        IReadOnlyDictionary<string, SpeciesTraceRule> ruleMap)
    {
        var groupedParticipants = participants
            .Select((item, index) => new { Participant = item, Index = index })
            .GroupBy(item => item.Participant.SpeciesName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Min(item => item.Index))
            .Select(group => new ReactionParticipant(
                group.First().Participant.SpeciesName,
                group.Sum(item => item.Participant.StoichiometricCoefficient)))
            .ToArray();

        var sideStates = new List<LabeledSideState> { new(string.Empty, 0, 1) };

        foreach (var participant in groupedParticipants)
        {
            var groupStates = ExpandParticipantGroup(participant, ruleMap);
            sideStates = sideStates
                .SelectMany(existing => groupStates.Select(groupState => new LabeledSideState(
                    JoinSides(existing.EquationSide, groupState.EquationSide),
                    existing.MarkedAtomCount + groupState.MarkedAtomCount,
                    existing.AssignmentMultiplicity * groupState.AssignmentMultiplicity)))
                .ToList();
        }

        return sideStates;
    }

    private static IReadOnlyList<LabeledSideState> ExpandParticipantGroup(
        ReactionParticipant participant,
        IReadOnlyDictionary<string, SpeciesTraceRule> ruleMap)
    {
        var moleculeStates = new List<MarkedSpeciesState> { new(participant.SpeciesName, 0) };
        if (ruleMap.TryGetValue(participant.SpeciesName, out var rule))
        {
            moleculeStates.AddRange(rule.States);
        }

        var results = new List<LabeledSideState>();
        var stateCounts = new int[moleculeStates.Count];
        EnumerateStateCounts(0, participant.StoichiometricCoefficient, stateCounts, moleculeStates, results);
        return results;
    }

    private static void EnumerateStateCounts(
        int stateIndex,
        int remainingMolecules,
        int[] stateCounts,
        IReadOnlyList<MarkedSpeciesState> moleculeStates,
        ICollection<LabeledSideState> results)
    {
        if (stateIndex == moleculeStates.Count - 1)
        {
            stateCounts[stateIndex] = remainingMolecules;
            results.Add(BuildGroupState(stateCounts, moleculeStates));
            return;
        }

        for (var count = remainingMolecules; count >= 0; count--)
        {
            stateCounts[stateIndex] = count;
            EnumerateStateCounts(
                stateIndex + 1,
                remainingMolecules - count,
                stateCounts,
                moleculeStates,
                results);
        }
    }

    private static LabeledSideState BuildGroupState(
        IReadOnlyList<int> stateCounts,
        IReadOnlyList<MarkedSpeciesState> moleculeStates)
    {
        var species = new List<string>();
        var markedAtoms = 0;
        var totalMolecules = stateCounts.Sum();
        var denominator = 1;

        for (var index = 0; index < stateCounts.Count; index++)
        {
            var count = stateCounts[index];
            markedAtoms += count * moleculeStates[index].MarkedAtomCount;
            denominator *= Factorial(count);

            for (var item = 0; item < count; item++)
            {
                species.Add(moleculeStates[index].Name);
            }
        }

        return new LabeledSideState(
            string.Join("+", species),
            markedAtoms,
            Factorial(totalMolecules) / denominator);
    }

    private static int Factorial(int value)
    {
        var result = 1;
        for (var factor = 2; factor <= value; factor++)
        {
            result *= factor;
        }

        return result;
    }

    private static string JoinSides(string first, string second)
    {
        if (first.Length == 0) return second;
        if (second.Length == 0) return first;
        return $"{first}+{second}";
    }

    private static string FormatEquation(
        string originalEquation,
        ParsedReactionEquation parsed,
        string reactants,
        string products)
    {
        if (originalEquation.Contains("(+M)", StringComparison.OrdinalIgnoreCase))
        {
            reactants += "(+M)";
            products += "(+M)";
        }
        else if (HasExplicitThirdBody(originalEquation))
        {
            reactants += "+M";
            products += "+M";
        }

        return $"{reactants}{parsed.ArrowText}{products}";
    }

    private static bool HasExplicitThirdBody(string equation)
    {
        return equation
            .Replace("(+M)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Split(["<=>", "=>", "="], StringSplitOptions.None)
            .Any(side => side.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(token => string.Equals(token, "M", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool ContainsElement(
        string speciesName,
        IReadOnlyDictionary<string, SpeciesInfo> speciesByName,
        string elementSymbol)
    {
        return speciesByName.TryGetValue(speciesName, out var species) && species.ContainsElement(elementSymbol);
    }

    private static int CountElementAtoms(
        IEnumerable<ReactionParticipant> participants,
        IReadOnlyDictionary<string, SpeciesInfo> speciesByName,
        string elementSymbol)
    {
        return participants.Sum(participant =>
        {
            if (!speciesByName.TryGetValue(participant.SpeciesName, out var species) ||
                !species.ElementCounts.TryGetValue(elementSymbol, out var atomCount))
            {
                return 0;
            }

            return participant.StoichiometricCoefficient * atomCount;
        });
    }
}
