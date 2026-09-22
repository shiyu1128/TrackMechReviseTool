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
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(
                "The rewrite plan contains validation errors. Correct the plan before writing reactions.");
        }

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
            if (!string.Equals(row.PlanStatus, "Ready", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Reaction {row.SourceReactionIndex}, candidate {row.CandidateIndex} is not ready to write: {row.ValidationMessage}");
            }
            if (!reactionsByIndex.TryGetValue(row.SourceReactionIndex, out var sourceReaction))
            {
                throw new InvalidOperationException(
                    $"Source reaction {row.SourceReactionIndex} does not exist in the parsed .out mechanism.");
            }
            if (!row.ForwardA.HasValue || !row.ForwardRateMultiplier.HasValue)
            {
                throw new InvalidOperationException(
                    $"Reaction {row.SourceReactionIndex}, candidate {row.CandidateIndex} has no calculated forward rate.");
            }

            builder.Append("! Source reaction ")
                .Append(row.SourceReactionIndex.ToString(CultureInfo.InvariantCulture))
                .Append(": ")
                .AppendLine(row.SourceEquation);
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

            if (row.CopyLow && sourceReaction.LowPressureLimit.Count >= 3)
            {
                builder.Append("    LOW / ")
                    .Append(FormatA(sourceReaction.LowPressureLimit[0] * row.ForwardRateMultiplier.Value)).Append(' ')
                    .Append(FormatParameter(sourceReaction.LowPressureLimit[1])).Append(' ')
                    .Append(FormatParameter(sourceReaction.LowPressureLimit[2]))
                    .AppendLine(" /");
            }

            if (row.CopyTroe && sourceReaction.TroeCentering.Count > 0)
            {
                builder.Append("    TROE / ")
                    .Append(string.Join(' ', sourceReaction.TroeCentering.Select(FormatParameter)))
                    .AppendLine(" /");
            }

            if (row.CopyColliderEfficiencies && sourceReaction.ColliderEfficiencies.Count > 0)
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

            if (row.IsDuplicate)
            {
                builder.AppendLine("    DUPLICATE");
            }

            builder.AppendLine();
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
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
