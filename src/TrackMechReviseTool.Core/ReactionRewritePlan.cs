namespace TrackMechReviseTool.Core;

public sealed record ReactionRewritePlanRow(
    int SourceReactionIndex,
    string SourceEquation,
    double SourceA,
    double SourceN,
    double SourceE,
    int CandidateIndex,
    int MarkedAtomCount,
    string CandidateEquation,
    bool Selected,
    string SelectionMode,
    string ForwardBranchGroup,
    int ForwardBranchCount,
    int ForwardAssignmentMultiplicity,
    double? ForwardProbability,
    double? ForwardRateMultiplier,
    double? ForwardA,
    double ForwardN,
    double ForwardE,
    string ReverseBranchGroup,
    int ReverseBranchCount,
    int ReverseAssignmentMultiplicity,
    double? ReverseProbability,
    double? ReverseRateMultiplier,
    string ExplicitRevRequirement,
    double? RevA,
    double? RevN,
    double? RevE,
    bool CopyLow,
    bool CopyTroe,
    bool CopyColliderEfficiencies,
    bool IsDuplicate,
    string PlanStatus,
    string ValidationMessage);

public sealed class ReactionRewritePlanService
{
    private const double MultiplierTolerance = 1e-12;

    public IReadOnlyList<ReactionRewritePlanRow> BuildRows(OutMechanism mechanism, string elementSymbol)
    {
        var ruleMap = new TraceRuleCatalog()
            .GetRules(elementSymbol)
            .ToDictionary(item => item.SourceSpecies, StringComparer.OrdinalIgnoreCase);
        var generator = new LabeledReactionTypeGenerator();
        var rows = new List<ReactionRewritePlanRow>();

        foreach (var reaction in mechanism.Reactions)
        {
            var expansion = generator.Generate(mechanism, reaction, elementSymbol, ruleMap);
            if (expansion.TotalElementAtomCount == 0 || expansion.Candidates.Count == 0)
            {
                continue;
            }

            rows.AddRange(BuildReactionRows(reaction, expansion));
        }

        return rows;
    }

    private static IReadOnlyList<ReactionRewritePlanRow> BuildReactionRows(
        ReactionInfo reaction,
        LabeledReactionExpansion expansion)
    {
        var forwardGroups = BuildGroupMap(
            reaction.Index,
            "F",
            expansion.Candidates,
            candidate => candidate.ReactantState);
        var reverseGroups = BuildGroupMap(
            reaction.Index,
            "R",
            expansion.Candidates,
            candidate => candidate.ProductState);
        var automaticSelection = expansion.IsComplete && forwardGroups.Values.All(group => group.Count == 1);
        var selectionMode = !expansion.IsComplete
            ? "IncompleteSpeciesRules"
            : automaticSelection ? "Automatic" : "UserSelectionRequired";
        var rows = new List<ReactionRewritePlanRow>();

        for (var index = 0; index < expansion.Candidates.Count; index++)
        {
            var candidate = expansion.Candidates[index];
            var forwardGroup = forwardGroups[GroupKey(candidate.MarkedAtomCount, candidate.ReactantState)];
            var reverseGroup = reverseGroups[GroupKey(candidate.MarkedAtomCount, candidate.ProductState)];
            var selected = automaticSelection;
            double? forwardProbability = selected && forwardGroup.Count == 1 ? 1.0 : null;
            double? reverseProbability = selected && reverseGroup.Count == 1 ? 1.0 : null;
            var forwardMultiplier = Multiply(candidate.ForwardAssignmentMultiplicity, forwardProbability);
            var reverseMultiplier = Multiply(candidate.ReverseAssignmentMultiplicity, reverseProbability);
            var explicitRev = ExplicitRevRequirement(expansion.ArrowKind, forwardMultiplier, reverseMultiplier);
            var planStatus = PlanStatus(
                expansion.IsComplete,
                selected,
                forwardProbability,
                reverseProbability,
                explicitRev);

            rows.Add(new ReactionRewritePlanRow(
                reaction.Index,
                reaction.Equation,
                reaction.Rate.A,
                reaction.Rate.B,
                reaction.Rate.Ea,
                index + 1,
                candidate.MarkedAtomCount,
                candidate.Equation,
                selected,
                selectionMode,
                forwardGroup.Id,
                forwardGroup.Count,
                candidate.ForwardAssignmentMultiplicity,
                forwardProbability,
                forwardMultiplier,
                forwardMultiplier.HasValue ? reaction.Rate.A * forwardMultiplier.Value : null,
                reaction.Rate.B,
                reaction.Rate.Ea,
                reverseGroup.Id,
                reverseGroup.Count,
                candidate.ReverseAssignmentMultiplicity,
                reverseProbability,
                reverseMultiplier,
                explicitRev,
                null,
                null,
                null,
                reaction.LowPressureLimit.Count > 0,
                reaction.TroeCentering.Count > 0,
                reaction.ColliderEfficiencies.Count > 0,
                reaction.IsDuplicate,
                planStatus,
                ValidationMessage(planStatus, expansion.MissingSpeciesRules)));
        }

        return rows;
    }

    private static IReadOnlyDictionary<string, BranchGroup> BuildGroupMap(
        int reactionIndex,
        string direction,
        IReadOnlyList<LabeledReactionCandidate> candidates,
        Func<LabeledReactionCandidate, string> stateSelector)
    {
        var groups = new Dictionary<string, BranchGroup>(StringComparer.Ordinal);
        var nextGroupIndex = 1;

        foreach (var group in candidates.GroupBy(candidate => GroupKey(candidate.MarkedAtomCount, stateSelector(candidate))))
        {
            var first = group.First();
            groups[group.Key] = new BranchGroup(
                $"RXN{reactionIndex}-K{first.MarkedAtomCount}-{direction}{nextGroupIndex}",
                group.Count());
            nextGroupIndex++;
        }

        return groups;
    }

    private static string GroupKey(int markedAtomCount, string state)
    {
        return $"{markedAtomCount}\u001F{state}";
    }

    private static double? Multiply(int multiplicity, double? probability)
    {
        return probability.HasValue ? multiplicity * probability.Value : null;
    }

    private static string ExplicitRevRequirement(
        ReactionArrowKind arrowKind,
        double? forwardMultiplier,
        double? reverseMultiplier)
    {
        if (arrowKind == ReactionArrowKind.Irreversible)
        {
            return "REV_NOT_APPLICABLE";
        }
        if (!forwardMultiplier.HasValue || !reverseMultiplier.HasValue)
        {
            return "REV_CHECK_AFTER_PROBABILITIES";
        }

        return Math.Abs(forwardMultiplier.Value - reverseMultiplier.Value) <= MultiplierTolerance
            ? "REV_NOT_REQUIRED"
            : "REV_REQUIRED";
    }

    private static string PlanStatus(
        bool expansionComplete,
        bool selected,
        double? forwardProbability,
        double? reverseProbability,
        string explicitRevRequirement)
    {
        if (!expansionComplete) return "BlockedMissingSpeciesRules";
        if (!selected) return "NeedsSelection";
        if (!forwardProbability.HasValue || !reverseProbability.HasValue) return "NeedsProbabilities";
        if (string.Equals(explicitRevRequirement, "REV_REQUIRED", StringComparison.Ordinal))
        {
            return "NeedsRevParameters";
        }

        return "Ready";
    }

    private static string ValidationMessage(string planStatus, IReadOnlyList<string> missingSpeciesRules)
    {
        return planStatus switch
        {
            "BlockedMissingSpeciesRules" => $"Add trace rules for: {string.Join(", ", missingSpeciesRules)}",
            "NeedsSelection" => "Select allowed atom mappings, then enter branch probabilities",
            "NeedsProbabilities" => "Enter forward and reverse probabilities; each branch group must sum to 1",
            "NeedsRevParameters" => "Forward and reverse multipliers differ; enter REV A, n, E",
            "Ready" => "Ready to write",
            _ => string.Empty
        };
    }

    private sealed record BranchGroup(string Id, int Count);
}
