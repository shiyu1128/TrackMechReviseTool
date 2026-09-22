using System.Globalization;
using System.Text;

namespace TrackMechReviseTool.Core;

public enum PlanValidationSeverity
{
    Warning,
    Error
}

public sealed record PlanValidationIssue(
    PlanValidationSeverity Severity,
    string Code,
    int SourceReactionIndex,
    string Location,
    string Message);

public sealed record ReactionRewritePlanValidationResult(
    IReadOnlyList<ReactionRewritePlanRow> NormalizedRows,
    IReadOnlyList<PlanValidationIssue> Issues)
{
    public bool IsValid => Issues.All(issue => issue.Severity != PlanValidationSeverity.Error);
}

public sealed class ReactionRewritePlanCsvReader
{
    public IReadOnlyList<ReactionRewritePlanRow> Read(string path)
    {
        var records = CsvRecordParser.Parse(File.ReadAllText(path, Encoding.UTF8));
        if (records.Count == 0)
        {
            return [];
        }

        var headers = records[0]
            .Select((name, index) => new { Name = name.TrimStart('\uFEFF'), Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);
        var rows = new List<ReactionRewritePlanRow>();

        for (var recordIndex = 1; recordIndex < records.Count; recordIndex++)
        {
            var record = records[recordIndex];
            if (record.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var rowNumber = recordIndex + 1;
            string Value(string name)
            {
                if (!headers.TryGetValue(name, out var columnIndex))
                {
                    throw new FormatException($"Rewrite plan is missing required column '{name}'.");
                }
                if (columnIndex >= record.Count)
                {
                    throw new FormatException($"Row {rowNumber} has no value for column '{name}'.");
                }

                return record[columnIndex].Trim();
            }

            rows.Add(new ReactionRewritePlanRow(
                ParseInt(Value("SourceReactionIndex"), rowNumber, "SourceReactionIndex"),
                Value("SourceEquation"),
                ParseDouble(Value("SourceA"), rowNumber, "SourceA"),
                ParseDouble(Value("SourceN"), rowNumber, "SourceN"),
                ParseDouble(Value("SourceE"), rowNumber, "SourceE"),
                ParseInt(Value("CandidateIndex"), rowNumber, "CandidateIndex"),
                ParseInt(Value("MarkedAtomCount"), rowNumber, "MarkedAtomCount"),
                Value("CandidateEquation"),
                ParseBool(Value("Selected"), rowNumber, "Selected"),
                Value("SelectionMode"),
                Value("ForwardBranchGroup"),
                ParseInt(Value("ForwardBranchCount"), rowNumber, "ForwardBranchCount"),
                ParseInt(Value("gf"), rowNumber, "gf"),
                ParseNullableDouble(Value("ForwardProbability"), rowNumber, "ForwardProbability"),
                ParseNullableDouble(Value("ForwardRateMultiplier"), rowNumber, "ForwardRateMultiplier"),
                ParseNullableDouble(Value("ForwardA"), rowNumber, "ForwardA"),
                ParseDouble(Value("ForwardN"), rowNumber, "ForwardN"),
                ParseDouble(Value("ForwardE"), rowNumber, "ForwardE"),
                Value("ReverseBranchGroup"),
                ParseInt(Value("ReverseBranchCount"), rowNumber, "ReverseBranchCount"),
                ParseInt(Value("gr"), rowNumber, "gr"),
                ParseNullableDouble(Value("ReverseProbability"), rowNumber, "ReverseProbability"),
                ParseNullableDouble(Value("ReverseRateMultiplier"), rowNumber, "ReverseRateMultiplier"),
                Value("ExplicitRevRequirement"),
                ParseNullableDouble(Value("RevA"), rowNumber, "RevA"),
                ParseNullableDouble(Value("RevN"), rowNumber, "RevN"),
                ParseNullableDouble(Value("RevE"), rowNumber, "RevE"),
                ParseBool(Value("CopyLOW"), rowNumber, "CopyLOW"),
                ParseBool(Value("CopyTROE"), rowNumber, "CopyTROE"),
                ParseBool(Value("CopyColliderEfficiencies"), rowNumber, "CopyColliderEfficiencies"),
                ParseBool(Value("Duplicate"), rowNumber, "Duplicate"),
                Value("PlanStatus"),
                Value("ValidationMessage")));
        }

        return rows;
    }

    private static int ParseInt(string value, int row, string column)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new FormatException($"Row {row}, column '{column}' is not a valid integer: '{value}'.");
    }

    private static double ParseDouble(string value, int row, string column)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new FormatException($"Row {row}, column '{column}' is not a valid number: '{value}'.");
    }

    private static double? ParseNullableDouble(string value, int row, string column)
    {
        return value.Length == 0 ? null : ParseDouble(value, row, column);
    }

    private static bool ParseBool(string value, int row, string column)
    {
        if (bool.TryParse(value, out var result)) return result;
        if (value == "1") return true;
        if (value == "0") return false;
        throw new FormatException($"Row {row}, column '{column}' is not a valid boolean: '{value}'.");
    }

    private static class CsvRecordParser
    {
        public static IReadOnlyList<IReadOnlyList<string>> Parse(string text)
        {
            var records = new List<IReadOnlyList<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;

            for (var index = 0; index < text.Length; index++)
            {
                var current = text[index];
                if (current == '"')
                {
                    if (inQuotes && index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (!inQuotes && current == ',')
                {
                    record.Add(field.ToString());
                    field.Clear();
                    continue;
                }

                if (!inQuotes && (current == '\r' || current == '\n'))
                {
                    record.Add(field.ToString());
                    field.Clear();
                    records.Add(record);
                    record = [];
                    if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }
                    continue;
                }

                field.Append(current);
            }

            if (inQuotes)
            {
                throw new FormatException("Rewrite plan CSV contains an unterminated quoted field.");
            }
            if (field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }

            return records;
        }
    }
}

public sealed class ReactionRewritePlanValidator
{
    private const double ProbabilityTolerance = 1e-9;
    private readonly ReactionEquationParser equationParser = new();

    public ReactionRewritePlanValidationResult Validate(IEnumerable<ReactionRewritePlanRow> inputRows)
    {
        var rows = inputRows.ToArray();
        var issues = new List<PlanValidationIssue>();
        var normalizedRows = rows.Select(NormalizeRow).ToArray();

        ValidateUniqueCandidates(normalizedRows, issues);
        ValidateMissingRules(normalizedRows, issues);
        ValidateForwardGroups(normalizedRows, issues);
        ValidateReverseGroups(normalizedRows, issues);
        ValidateSelectedRows(normalizedRows, issues);
        var finalRows = ApplyIssueStatuses(normalizedRows, issues);

        return new ReactionRewritePlanValidationResult(finalRows, issues);
    }

    private static IReadOnlyList<ReactionRewritePlanRow> ApplyIssueStatuses(
        IReadOnlyList<ReactionRewritePlanRow> rows,
        IReadOnlyList<PlanValidationIssue> issues)
    {
        return rows.Select(row =>
        {
            var candidateLocation = $"candidate {row.CandidateIndex}";
            var rowErrors = issues
                .Where(issue => issue.Severity == PlanValidationSeverity.Error)
                .Where(issue => issue.SourceReactionIndex == row.SourceReactionIndex)
                .Where(issue =>
                    issue.Location == "reaction" ||
                    issue.Location == candidateLocation ||
                    issue.Location == row.ForwardBranchGroup ||
                    issue.Location == row.ReverseBranchGroup)
                .Select(issue => issue.Message)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return rowErrors.Length == 0
                ? row
                : row with
                {
                    PlanStatus = "Invalid",
                    ValidationMessage = string.Join(" | ", rowErrors)
                };
        }).ToArray();
    }

    private ReactionRewritePlanRow NormalizeRow(ReactionRewritePlanRow row)
    {
        var arrowKind = equationParser.Parse(row.SourceEquation).ArrowKind;
        double? forwardMultiplier = row.Selected && row.ForwardProbability.HasValue
            ? row.ForwardAssignmentMultiplicity * row.ForwardProbability.Value
            : null;
        double? reverseMultiplier = row.Selected && arrowKind == ReactionArrowKind.Reversible && row.ReverseProbability.HasValue
            ? row.ReverseAssignmentMultiplicity * row.ReverseProbability.Value
            : null;
        var revRequirement = DetermineRevRequirement(arrowKind, forwardMultiplier, reverseMultiplier);
        var status = DetermineStatus(row, arrowKind, revRequirement);

        return row with
        {
            ForwardRateMultiplier = forwardMultiplier,
            ForwardA = forwardMultiplier.HasValue ? row.SourceA * forwardMultiplier.Value : null,
            ForwardN = row.SourceN,
            ForwardE = row.SourceE,
            ReverseRateMultiplier = reverseMultiplier,
            ExplicitRevRequirement = revRequirement,
            PlanStatus = status,
            ValidationMessage = StatusMessage(status)
        };
    }

    private static void ValidateUniqueCandidates(
        IReadOnlyList<ReactionRewritePlanRow> rows,
        ICollection<PlanValidationIssue> issues)
    {
        foreach (var duplicate in rows.GroupBy(row => (row.SourceReactionIndex, row.CandidateIndex)).Where(group => group.Count() > 1))
        {
            issues.Add(Error(
                "DUPLICATE_CANDIDATE_INDEX",
                duplicate.Key.SourceReactionIndex,
                $"candidate {duplicate.Key.CandidateIndex}",
                "Candidate index is duplicated within the source reaction."));
        }
    }

    private static void ValidateMissingRules(
        IReadOnlyList<ReactionRewritePlanRow> rows,
        ICollection<PlanValidationIssue> issues)
    {
        foreach (var reaction in rows
                     .Where(row => string.Equals(row.SelectionMode, "IncompleteSpeciesRules", StringComparison.Ordinal))
                     .GroupBy(row => row.SourceReactionIndex))
        {
            issues.Add(Error(
                "MISSING_SPECIES_RULES",
                reaction.Key,
                "reaction",
                "Trace-species definitions are incomplete for this reaction."));
        }
    }

    private static void ValidateForwardGroups(
        IReadOnlyList<ReactionRewritePlanRow> rows,
        ICollection<PlanValidationIssue> issues)
    {
        foreach (var group in rows
                     .Where(row => !string.Equals(row.SelectionMode, "IncompleteSpeciesRules", StringComparison.Ordinal))
                     .GroupBy(row => (row.SourceReactionIndex, row.ForwardBranchGroup)))
        {
            var selected = group.Where(row => row.Selected).ToArray();
            if (selected.Length == 0)
            {
                issues.Add(Error(
                    "NO_FORWARD_BRANCH_SELECTED",
                    group.Key.SourceReactionIndex,
                    group.Key.ForwardBranchGroup,
                    "Select at least one allowed product mapping for this labeled reactant state."));
                continue;
            }

            ValidateProbabilityGroup(
                selected,
                row => row.ForwardProbability,
                "FORWARD",
                group.Key.SourceReactionIndex,
                group.Key.ForwardBranchGroup,
                issues);
        }
    }

    private void ValidateReverseGroups(
        IReadOnlyList<ReactionRewritePlanRow> rows,
        ICollection<PlanValidationIssue> issues)
    {
        foreach (var group in rows
                     .Where(row => row.Selected && equationParser.Parse(row.SourceEquation).ArrowKind == ReactionArrowKind.Reversible)
                     .GroupBy(row => (row.SourceReactionIndex, row.ReverseBranchGroup)))
        {
            ValidateProbabilityGroup(
                group.ToArray(),
                row => row.ReverseProbability,
                "REVERSE",
                group.Key.SourceReactionIndex,
                group.Key.ReverseBranchGroup,
                issues);
        }
    }

    private static void ValidateProbabilityGroup(
        IReadOnlyList<ReactionRewritePlanRow> rows,
        Func<ReactionRewritePlanRow, double?> probabilitySelector,
        string direction,
        int reactionIndex,
        string groupName,
        ICollection<PlanValidationIssue> issues)
    {
        if (rows.Any(row => !probabilitySelector(row).HasValue))
        {
            issues.Add(Error(
                $"{direction}_PROBABILITY_MISSING",
                reactionIndex,
                groupName,
                $"Every selected {direction.ToLowerInvariant()} branch requires a probability."));
            return;
        }

        var probabilities = rows.Select(row => probabilitySelector(row)!.Value).ToArray();
        if (probabilities.Any(value => !double.IsFinite(value) || value < 0 || value > 1))
        {
            issues.Add(Error(
                $"{direction}_PROBABILITY_RANGE",
                reactionIndex,
                groupName,
                "Probabilities must be finite values between 0 and 1."));
            return;
        }

        var sum = probabilities.Sum();
        if (Math.Abs(sum - 1.0) > ProbabilityTolerance)
        {
            issues.Add(Error(
                $"{direction}_PROBABILITY_SUM",
                reactionIndex,
                groupName,
                $"Selected branch probabilities sum to {sum.ToString("G17", CultureInfo.InvariantCulture)} instead of 1."));
        }
    }

    private void ValidateSelectedRows(
        IReadOnlyList<ReactionRewritePlanRow> rows,
        ICollection<PlanValidationIssue> issues)
    {
        foreach (var row in rows.Where(item => item.Selected))
        {
            if (!double.IsFinite(row.SourceA) || !double.IsFinite(row.SourceN) || !double.IsFinite(row.SourceE))
            {
                issues.Add(Error(
                    "SOURCE_RATE_NOT_FINITE",
                    row.SourceReactionIndex,
                    $"candidate {row.CandidateIndex}",
                    "Source A, n and E must be finite .out values."));
            }

            var arrowKind = equationParser.Parse(row.SourceEquation).ArrowKind;
            if (arrowKind == ReactionArrowKind.Reversible &&
                string.Equals(row.ExplicitRevRequirement, "REV_REQUIRED", StringComparison.Ordinal) &&
                (!row.RevA.HasValue || !row.RevN.HasValue || !row.RevE.HasValue))
            {
                issues.Add(Error(
                    "REV_PARAMETERS_MISSING",
                    row.SourceReactionIndex,
                    $"candidate {row.CandidateIndex}",
                    "Manual input required: enter reverse Arrhenius RevA, RevN and RevE. REV / A n E / will be written immediately after this reaction."));
            }

            if (new[] { row.RevA, row.RevN, row.RevE }
                .Where(value => value.HasValue)
                .Any(value => !double.IsFinite(value!.Value)))
            {
                issues.Add(Error(
                    "REV_PARAMETER_NOT_FINITE",
                    row.SourceReactionIndex,
                    $"candidate {row.CandidateIndex}",
                    "REV parameters must be finite numbers."));
            }
        }
    }

    private string DetermineStatus(
        ReactionRewritePlanRow row,
        ReactionArrowKind arrowKind,
        string revRequirement)
    {
        if (string.Equals(row.SelectionMode, "IncompleteSpeciesRules", StringComparison.Ordinal))
        {
            return "BlockedMissingSpeciesRules";
        }
        if (!row.Selected) return "Excluded";
        if (!row.ForwardProbability.HasValue ||
            (arrowKind == ReactionArrowKind.Reversible && !row.ReverseProbability.HasValue))
        {
            return "NeedsProbabilities";
        }
        if (string.Equals(revRequirement, "REV_REQUIRED", StringComparison.Ordinal) &&
            (!row.RevA.HasValue || !row.RevN.HasValue || !row.RevE.HasValue))
        {
            return "NeedsRevParameters";
        }

        return "Ready";
    }

    private static string DetermineRevRequirement(
        ReactionArrowKind arrowKind,
        double? forwardMultiplier,
        double? reverseMultiplier)
    {
        if (arrowKind == ReactionArrowKind.Irreversible) return "REV_NOT_APPLICABLE";
        if (!forwardMultiplier.HasValue || !reverseMultiplier.HasValue)
        {
            return "REV_CHECK_AFTER_PROBABILITIES";
        }

        return Math.Abs(forwardMultiplier.Value - reverseMultiplier.Value) <= ProbabilityTolerance
            ? "REV_NOT_REQUIRED"
            : "REV_REQUIRED";
    }

    private static string StatusMessage(string status)
    {
        return status switch
        {
            "BlockedMissingSpeciesRules" => "Add the missing marked-species definitions before writing",
            "Excluded" => "Candidate is not selected",
            "NeedsProbabilities" => "Enter forward and reverse probabilities",
            "NeedsRevParameters" => "Manual input required: enter reverse Arrhenius RevA, RevN and RevE; REV is written immediately after this reaction",
            "Ready" => "Ready to write",
            _ => string.Empty
        };
    }

    private static PlanValidationIssue Error(string code, int reaction, string location, string message)
    {
        return new PlanValidationIssue(PlanValidationSeverity.Error, code, reaction, location, message);
    }
}
