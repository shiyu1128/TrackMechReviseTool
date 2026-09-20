namespace TrackMechReviseTool.Core;

public sealed record ReactionReviewRow(
    int ReactionIndex,
    string Equation,
    double A,
    double B,
    double Ea,
    string ReactionType,
    string MatchedSpecies,
    string ProposedRule,
    int TargetElementAtomCount,
    string LabelCountLayers,
    int CandidateMarkedReactionTypeCount,
    string ReactionTypeCountStatus,
    bool ReactionTypeExpansionComplete,
    string CandidateMarkedReactions,
    string MissingSpeciesRules,
    string BranchProbabilityRequirement,
    string RecommendedForwardOperation,
    string RecommendedReverseOperation,
    string ExplicitRevRequirement,
    string ExplicitRevReason,
    string ForwardOperationOptions,
    string ReverseOperationOptions,
    string RequiredUserInputs,
    string ReviewStatus,
    string Notes);

public sealed class ReactionReviewService
{
    public IReadOnlyList<ReactionReviewRow> BuildReviewRows(OutMechanism mechanism, string elementSymbol)
    {
        var ruleCatalog = new TraceRuleCatalog();
        var rules = ruleCatalog.GetRules(elementSymbol);
        var ruleMap = rules.ToDictionary(item => item.SourceSpecies, StringComparer.OrdinalIgnoreCase);
        var assessmentService = new BranchProbabilityAssessmentService();
        var reactionTypeGenerator = new LabeledReactionTypeGenerator();

        return mechanism.Reactions
            .Select(reaction => BuildReviewRow(
                mechanism,
                reaction,
                elementSymbol,
                ruleMap,
                assessmentService,
                reactionTypeGenerator))
            .ToList();
    }

    private static ReactionReviewRow BuildReviewRow(
        OutMechanism mechanism,
        ReactionInfo reaction,
        string elementSymbol,
        IReadOnlyDictionary<string, SpeciesTraceRule> ruleMap,
        BranchProbabilityAssessmentService assessmentService,
        LabeledReactionTypeGenerator reactionTypeGenerator)
    {
        var species = ReactionSpeciesTokens(reaction.Equation)
            .Where(ruleMap.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToArray();

        var reactionType = ReactionType(reaction);
        var proposedRule = species.Length == 0
            ? "No target-element species from current rule table"
            : string.Join("; ", species.Select(name => $"{name}->{string.Join("/", ruleMap[name].MarkedSpecies)}"));
        var assessment = assessmentService.Assess(mechanism, reaction, elementSymbol, ruleMap);
        var expansion = reactionTypeGenerator.Generate(mechanism, reaction, elementSymbol, ruleMap);
        var explicitRev = AssessExplicitRev(expansion);
        var recommendedReverseOperation = explicitRev.Requirement == "REV_REQUIRED"
            ? ReverseRewriteOperationCatalog.ScaleOriginalReverse
            : assessment.RecommendedReverseOperationId;

        var status = assessment.Requirement == BranchProbabilityRequirement.NotApplicable
            ? "Skip"
            : NeedsManualReview(reaction, assessment, expansion) ? "ManualReview" : "AutoCandidate";

        var notes = new List<string>();
        if (reaction.IsDuplicate) notes.Add("duplicate");
        if (reaction.LowPressureLimit.Count > 0) notes.Add("copy LOW");
        if (reaction.TroeCentering.Count > 0) notes.Add("copy TROE");
        if (reaction.ColliderEfficiencies.Count > 0) notes.Add("copy collider efficiencies");
        if (assessment.Requirement == BranchProbabilityRequirement.Required)
        {
            notes.Add("branch probabilities required");
        }
        if (assessment.Requirement == BranchProbabilityRequirement.AtomBalanceError)
        {
            notes.Add("target-element atom balance error");
        }
        if (!expansion.IsComplete)
        {
            notes.Add($"missing marked-species rules: {string.Join(", ", expansion.MissingSpeciesRules)}");
        }
        notes.AddRange(assessment.Notes);
        if (reaction.Warnings.Count > 0) notes.Add(string.Join(" | ", reaction.Warnings));

        return new ReactionReviewRow(
            reaction.Index,
            reaction.Equation,
            reaction.Rate.A,
            reaction.Rate.B,
            reaction.Rate.Ea,
            reactionType,
            string.Join(" ", species),
            proposedRule,
            expansion.TotalElementAtomCount,
            string.Join(";", expansion.Layers.Select(layer =>
                $"k={layer.MarkedAtomCount}:R={layer.ReactantStateCount}/P={layer.ProductStateCount}/T={layer.CandidateReactionCount}")),
            expansion.AddedReactionTypeCount,
            ReactionTypeCountStatus(expansion),
            expansion.IsComplete,
            string.Join(" || ", expansion.Candidates.Select(candidate =>
                FormatCandidate(candidate, expansion.Candidates, reaction.Rate))),
            string.Join(" ", expansion.MissingSpeciesRules),
            assessment.Requirement.ToString(),
            assessment.RecommendedForwardOperationId,
            recommendedReverseOperation,
            explicitRev.Requirement,
            explicitRev.Reason,
            string.Join("|", assessment.ForwardOperationIds),
            string.Join("|", assessment.ReverseOperationIds),
            string.Join("|", assessment.RequiredUserInputs),
            status,
            string.Join("; ", notes));
    }

    private static string ReactionTypeCountStatus(LabeledReactionExpansion expansion)
    {
        if (expansion.TotalElementAtomCount == 0)
        {
            return "NotApplicable";
        }
        if (!expansion.IsComplete)
        {
            return "IncompleteSpeciesRules";
        }

        var hasCompetingMappings = expansion.Candidates
            .GroupBy(item => (item.MarkedAtomCount, item.ReactantState))
            .Any(group => group.Count() > 1);
        return hasCompetingMappings ? "RequiresBranchOrAtomMapping" : "Automatic";
    }

    private static string FormatCandidate(
        LabeledReactionCandidate candidate,
        IReadOnlyList<LabeledReactionCandidate> allCandidates,
        ArrheniusRate originalRate)
    {
        var forwardBranches = allCandidates.Count(item =>
            item.MarkedAtomCount == candidate.MarkedAtomCount &&
            string.Equals(item.ReactantState, candidate.ReactantState, StringComparison.Ordinal));
        var reverseBranches = allCandidates.Count(item =>
            item.MarkedAtomCount == candidate.MarkedAtomCount &&
            string.Equals(item.ProductState, candidate.ProductState, StringComparison.Ordinal));
        var forwardFormula = forwardBranches == 1
            ? $"{candidate.ForwardAssignmentMultiplicity}*kf"
            : $"{candidate.ForwardAssignmentMultiplicity}*p_i*kf";
        var reverseFormula = reverseBranches == 1
            ? $"{candidate.ReverseAssignmentMultiplicity}*kr"
            : $"{candidate.ReverseAssignmentMultiplicity}*q_i*kr";
        var revStatus = forwardBranches == 1 && reverseBranches == 1
            ? candidate.ForwardAssignmentMultiplicity == candidate.ReverseAssignmentMultiplicity
                ? "REV_NOT_REQUIRED"
                : "REV_REQUIRED"
            : "REV_CHECK_AFTER_PROBABILITIES";
        var forwardRate = forwardBranches == 1
            ? $"Af*={(originalRate.A * candidate.ForwardAssignmentMultiplicity).ToString("G6", System.Globalization.CultureInfo.InvariantCulture)},n={originalRate.B.ToString("G6", System.Globalization.CultureInfo.InvariantCulture)},E={originalRate.Ea.ToString("G6", System.Globalization.CultureInfo.InvariantCulture)}"
            : "Af*=gf*p_i*A";

        return $"{candidate.Equation} [k={candidate.MarkedAtomCount},gf={candidate.ForwardAssignmentMultiplicity},bf={forwardBranches},kf*={forwardFormula},{forwardRate},gr={candidate.ReverseAssignmentMultiplicity},br={reverseBranches},kr*={reverseFormula},{revStatus}]";
    }

    private static (string Requirement, string Reason) AssessExplicitRev(LabeledReactionExpansion expansion)
    {
        if (expansion.ArrowKind == ReactionArrowKind.Irreversible)
        {
            return ("REV_NOT_APPLICABLE", "original reaction is irreversible");
        }

        var mismatches = new List<string>();
        var hasProbabilityDependentCandidate = false;

        foreach (var candidate in expansion.Candidates)
        {
            var forwardBranches = expansion.Candidates.Count(item =>
                item.MarkedAtomCount == candidate.MarkedAtomCount &&
                string.Equals(item.ReactantState, candidate.ReactantState, StringComparison.Ordinal));
            var reverseBranches = expansion.Candidates.Count(item =>
                item.MarkedAtomCount == candidate.MarkedAtomCount &&
                string.Equals(item.ProductState, candidate.ProductState, StringComparison.Ordinal));

            if (forwardBranches == 1 && reverseBranches == 1)
            {
                if (candidate.ForwardAssignmentMultiplicity != candidate.ReverseAssignmentMultiplicity)
                {
                    mismatches.Add(
                        $"{candidate.Equation}: forward factor {candidate.ForwardAssignmentMultiplicity}, reverse factor {candidate.ReverseAssignmentMultiplicity}");
                }
            }
            else
            {
                hasProbabilityDependentCandidate = true;
            }
        }

        if (mismatches.Count > 0)
        {
            return ("REV_REQUIRED", string.Join(" | ", mismatches));
        }
        if (hasProbabilityDependentCandidate)
        {
            return (
                "REV_CHECK_AFTER_PROBABILITIES",
                "compare gf*p_i with gr*q_i after forward and reverse probabilities are confirmed");
        }

        return ("REV_NOT_REQUIRED", "forward and reverse multipliers are equal");
    }

    private static bool NeedsManualReview(
        ReactionInfo reaction,
        BranchProbabilityAssessment assessment,
        LabeledReactionExpansion expansion)
    {
        return assessment.Requirement is BranchProbabilityRequirement.Required or BranchProbabilityRequirement.AtomBalanceError ||
            !expansion.IsComplete ||
            reaction.IsDuplicate;
    }

    private static string ReactionType(ReactionInfo reaction)
    {
        var types = new List<string>();
        if (reaction.Equation.Contains("(+M)", StringComparison.OrdinalIgnoreCase)) types.Add("falloff");
        if (reaction.Equation.Contains("+M", StringComparison.OrdinalIgnoreCase)) types.Add("third-body");
        if (reaction.LowPressureLimit.Count > 0) types.Add("LOW");
        if (reaction.TroeCentering.Count > 0) types.Add("TROE");
        if (reaction.IsDuplicate) types.Add("DUPLICATE");
        if (reaction.ColliderEfficiencies.Count > 0) types.Add("efficiencies");

        return types.Count == 0 ? "elementary" : string.Join("|", types);
    }

    private static IEnumerable<string> ReactionSpeciesTokens(string equation)
    {
        var normalizedEquation = RemoveThirdBodyDecorations(equation);
        return normalizedEquation
            .Replace("<=>", "+", StringComparison.Ordinal)
            .Replace("=>", "+", StringComparison.Ordinal)
            .Replace("=", "+", StringComparison.Ordinal)
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(RemoveLeadingStoichiometricCoefficient)
            .Where(token => token.Length > 0);
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
