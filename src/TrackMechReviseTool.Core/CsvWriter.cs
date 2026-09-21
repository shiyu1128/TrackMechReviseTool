using System.Globalization;
using System.Text;

namespace TrackMechReviseTool.Core;

public static class CsvWriter
{
    public static void WriteReactionReview(string path, IEnumerable<ReactionReviewRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Index,Equation,A,b,Ea,ReactionType,MatchedSpecies,ProposedRule,TargetElementAtomCount,LabelCountLayers,CandidateMarkedReactionTypeCount,ReactionTypeCountStatus,ReactionTypeExpansionComplete,CandidateMarkedReactions,MissingSpeciesRules,BranchProbabilityRequirement,RecommendedForwardOperation,RecommendedReverseOperation,ExplicitRevRequirement,ExplicitRevReason,ForwardOperationOptions,ReverseOperationOptions,RequiredUserInputs,ReviewStatus,Notes");

        foreach (var row in rows)
        {
            builder.Append(row.ReactionIndex.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(Escape(row.Equation)).Append(',');
            builder.Append(row.A.ToString("G17", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(row.B.ToString("G17", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(row.Ea.ToString("G17", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(Escape(row.ReactionType)).Append(',');
            builder.Append(Escape(row.MatchedSpecies)).Append(',');
            builder.Append(Escape(row.ProposedRule)).Append(',');
            builder.Append(row.TargetElementAtomCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(Escape(row.LabelCountLayers)).Append(',');
            builder.Append(row.CandidateMarkedReactionTypeCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(Escape(row.ReactionTypeCountStatus)).Append(',');
            builder.Append(row.ReactionTypeExpansionComplete ? "true" : "false").Append(',');
            builder.Append(Escape(row.CandidateMarkedReactions)).Append(',');
            builder.Append(Escape(row.MissingSpeciesRules)).Append(',');
            builder.Append(Escape(row.BranchProbabilityRequirement)).Append(',');
            builder.Append(Escape(row.RecommendedForwardOperation)).Append(',');
            builder.Append(Escape(row.RecommendedReverseOperation)).Append(',');
            builder.Append(Escape(row.ExplicitRevRequirement)).Append(',');
            builder.Append(Escape(row.ExplicitRevReason)).Append(',');
            builder.Append(Escape(row.ForwardOperationOptions)).Append(',');
            builder.Append(Escape(row.ReverseOperationOptions)).Append(',');
            builder.Append(Escape(row.RequiredUserInputs)).Append(',');
            builder.Append(Escape(row.ReviewStatus)).Append(',');
            builder.AppendLine(Escape(row.Notes));
        }

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
    }

    public static void WriteReactionRewritePlan(string path, IEnumerable<ReactionRewritePlanRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SourceReactionIndex,SourceEquation,SourceA,SourceN,SourceE,CandidateIndex,MarkedAtomCount,CandidateEquation,Selected,SelectionMode,ForwardBranchGroup,ForwardBranchCount,gf,ForwardProbability,ForwardRateMultiplier,ForwardA,ForwardN,ForwardE,ReverseBranchGroup,ReverseBranchCount,gr,ReverseProbability,ReverseRateMultiplier,ExplicitRevRequirement,RevA,RevN,RevE,CopyLOW,CopyTROE,CopyColliderEfficiencies,Duplicate,PlanStatus,ValidationMessage");

        foreach (var row in rows)
        {
            builder.Append(row.SourceReactionIndex.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(Escape(row.SourceEquation)).Append(',');
            builder.Append(row.SourceA.ToString("G17", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(row.SourceN.ToString("G17", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(row.SourceE.ToString("G17", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(row.CandidateIndex.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(row.MarkedAtomCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(Escape(row.CandidateEquation)).Append(',');
            builder.Append(row.Selected ? "true" : "false").Append(',');
            builder.Append(Escape(row.SelectionMode)).Append(',');
            builder.Append(Escape(row.ForwardBranchGroup)).Append(',');
            builder.Append(row.ForwardBranchCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(row.ForwardAssignmentMultiplicity.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(NullableDouble(row.ForwardProbability)).Append(',');
            builder.Append(NullableDouble(row.ForwardRateMultiplier)).Append(',');
            builder.Append(NullableDouble(row.ForwardA)).Append(',');
            builder.Append(row.ForwardN.ToString("G17", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(row.ForwardE.ToString("G17", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(Escape(row.ReverseBranchGroup)).Append(',');
            builder.Append(row.ReverseBranchCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(row.ReverseAssignmentMultiplicity.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(NullableDouble(row.ReverseProbability)).Append(',');
            builder.Append(NullableDouble(row.ReverseRateMultiplier)).Append(',');
            builder.Append(Escape(row.ExplicitRevRequirement)).Append(',');
            builder.Append(NullableDouble(row.RevA)).Append(',');
            builder.Append(NullableDouble(row.RevN)).Append(',');
            builder.Append(NullableDouble(row.RevE)).Append(',');
            builder.Append(row.CopyLow ? "true" : "false").Append(',');
            builder.Append(row.CopyTroe ? "true" : "false").Append(',');
            builder.Append(row.CopyColliderEfficiencies ? "true" : "false").Append(',');
            builder.Append(row.IsDuplicate ? "true" : "false").Append(',');
            builder.Append(Escape(row.PlanStatus)).Append(',');
            builder.AppendLine(Escape(row.ValidationMessage));
        }

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
    }

    private static string NullableDouble(double? value)
    {
        return value?.ToString("G17", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string Escape(string value)
    {
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }
}
