using System.Globalization;
using System.Text;

namespace TrackMechReviseTool.Core;

public sealed class ChemkinReactionSectionWriter
{
    public void Write(
        string path,
        OutMechanism mechanism,
        ReactionRewritePlanValidationResult validationResult)
    {
        EnsurePlanCanBeWritten(validationResult);

        var reactionsByIndex = mechanism.Reactions.ToDictionary(reaction => reaction.Index);
        var selectedRows = validationResult.NormalizedRows
            .Where(row => row.Selected)
            .OrderBy(row => row.SourceReactionIndex)
            .ThenBy(row => row.CandidateIndex)
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine("! Generated marked-reaction section. Review before merging into the complete mechanism.");
        builder.AppendLine("! REV placement rule: REV / A n E / is written immediately after its reaction rate line.");

        foreach (var row in selectedRows)
        {
            if (!reactionsByIndex.TryGetValue(row.SourceReactionIndex, out var sourceReaction))
            {
                throw new InvalidOperationException(
                    $"Source reaction {row.SourceReactionIndex} does not exist in the parsed .out mechanism.");
            }
            AppendMarkedReaction(builder, sourceReaction, row, includeSourceComment: true);
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
    }

    internal static void EnsurePlanCanBeWritten(ReactionRewritePlanValidationResult validationResult)
    {
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(
                "The rewrite plan contains validation errors. Correct the plan before writing reactions.");
        }

        var unreadyRow = validationResult.NormalizedRows.FirstOrDefault(row =>
            row.Selected && !string.Equals(row.PlanStatus, "Ready", StringComparison.Ordinal));
        if (unreadyRow is not null)
        {
            throw new InvalidOperationException(
                $"Reaction {unreadyRow.SourceReactionIndex}, candidate {unreadyRow.CandidateIndex} is not ready to write: {unreadyRow.ValidationMessage}");
        }
    }

    internal static void AppendSourceReaction(StringBuilder builder, ReactionInfo reaction)
    {
        WriteRateLine(builder, reaction.Equation, reaction.Rate.A, reaction.Rate.B, reaction.Rate.Ea);
        AppendAuxiliaryLines(
            builder,
            reaction,
            lowPressureMultiplier: 1.0,
            copyLow: reaction.LowPressureLimit.Count > 0,
            copyTroe: reaction.TroeCentering.Count > 0,
            copyColliderEfficiencies: reaction.ColliderEfficiencies.Count > 0,
            isDuplicate: reaction.IsDuplicate);
    }

    internal static void AppendMarkedReaction(
        StringBuilder builder,
        ReactionInfo sourceReaction,
        ReactionRewritePlanRow row,
        bool includeSourceComment)
    {
        if (!row.ForwardA.HasValue || !row.ForwardRateMultiplier.HasValue)
        {
            throw new InvalidOperationException(
                $"Reaction {row.SourceReactionIndex}, candidate {row.CandidateIndex} has no calculated forward rate.");
        }

        if (includeSourceComment)
        {
            builder.Append("! Source reaction ")
                .Append(row.SourceReactionIndex.ToString(CultureInfo.InvariantCulture))
                .Append(": ")
                .AppendLine(row.SourceEquation);
        }

        WriteRateLine(builder, row.CandidateEquation, row.ForwardA.Value, row.ForwardN, row.ForwardE);
        if (string.Equals(row.ExplicitRevRequirement, "REV_REQUIRED", StringComparison.Ordinal))
        {
            if (!row.RevA.HasValue || !row.RevN.HasValue || !row.RevE.HasValue)
            {
                throw new InvalidOperationException(
                    $"Reaction {row.SourceReactionIndex}, candidate {row.CandidateIndex} requires manual REV A, n and E values.");
            }

            builder.Append("    REV / ")
                .Append(FormatA(row.RevA.Value)).Append(' ')
                .Append(FormatParameter(row.RevN.Value)).Append(' ')
                .Append(FormatParameter(row.RevE.Value))
                .AppendLine(" /");
        }

        AppendAuxiliaryLines(
            builder,
            sourceReaction,
            row.ForwardRateMultiplier.Value,
            row.CopyLow,
            row.CopyTroe,
            row.CopyColliderEfficiencies,
            row.IsDuplicate);
    }

    private static void AppendAuxiliaryLines(
        StringBuilder builder,
        ReactionInfo sourceReaction,
        double lowPressureMultiplier,
        bool copyLow,
        bool copyTroe,
        bool copyColliderEfficiencies,
        bool isDuplicate)
    {
        if (copyLow && sourceReaction.LowPressureLimit.Count >= 3)
        {
            builder.Append("    LOW / ")
                .Append(FormatA(sourceReaction.LowPressureLimit[0] * lowPressureMultiplier)).Append(' ')
                .Append(FormatParameter(sourceReaction.LowPressureLimit[1])).Append(' ')
                .Append(FormatParameter(sourceReaction.LowPressureLimit[2]))
                .AppendLine(" /");
        }

        if (copyTroe && sourceReaction.TroeCentering.Count > 0)
        {
            builder.Append("    TROE / ")
                .Append(string.Join(' ', sourceReaction.TroeCentering.Select(FormatParameter)))
                .AppendLine(" /");
        }

        if (copyColliderEfficiencies && sourceReaction.ColliderEfficiencies.Count > 0)
        {
            builder.Append("    ");
            foreach (var efficiency in sourceReaction.ColliderEfficiencies)
            {
                builder.Append(efficiency.Key)
                    .Append(" / ")
                    .Append(FormatParameter(efficiency.Value))
                    .Append(" / ");
            }
            builder.AppendLine();
        }

        if (isDuplicate)
        {
            builder.AppendLine("    DUPLICATE");
        }

        builder.AppendLine();
    }

    private static void WriteRateLine(StringBuilder builder, string equation, double a, double n, double e)
    {
        builder.Append(equation.PadRight(52))
            .Append(' ')
            .Append(FormatA(a).PadLeft(18))
            .Append(' ')
            .Append(FormatParameter(n).PadLeft(12))
            .Append(' ')
            .AppendLine(FormatParameter(e).PadLeft(16));
    }

    private static string FormatA(double value)
    {
        return value.ToString("0.###############E+00", CultureInfo.InvariantCulture);
    }

    private static string FormatParameter(double value)
    {
        return value.ToString("G15", CultureInfo.InvariantCulture);
    }
}
