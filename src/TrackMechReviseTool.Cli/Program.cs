using TrackMechReviseTool.Core;

if (args.Length > 0 && string.Equals(args[0], "validate-plan", StringComparison.OrdinalIgnoreCase))
{
    return ValidatePlan(args);
}

var path = args.Length > 0 ? args[0] : "MODIFY-18_gas.out";
var element = args.Length > 1 ? args[1] : "O";
var reviewCsvPath = args.Length > 2 ? args[2] : string.Empty;
var rewritePlanCsvPath = args.Length > 3 ? args[3] : string.Empty;

if (!File.Exists(path))
{
    Console.Error.WriteLine($"File not found: {path}");
    return 2;
}

var parser = new OutMechanismParser();
var mechanism = parser.Parse(path);
var analyzer = new MechanismAnalyzer();
var markedSpecies = analyzer.SpeciesContainingElement(mechanism, element);
var affectedReactions = analyzer.ReactionsMentioningSpecies(mechanism, markedSpecies);
var reviewRows = new ReactionReviewService().BuildReviewRows(mechanism, element);
var rewritePlanRows = new ReactionRewritePlanService().BuildRows(mechanism, element);

Console.WriteLine("TrackMechReviseTool phase 1 parser summary");
Console.WriteLine($"Input: {Path.GetFullPath(path)}");
Console.WriteLine($"Elements: {string.Join(", ", mechanism.Elements.Select(item => item.Symbol))}");
Console.WriteLine($"Markable elements: {string.Join(", ", mechanism.MarkableElements())}");
Console.WriteLine($"Species: {mechanism.Species.Count}");
Console.WriteLine($"Reactions: {mechanism.Reactions.Count}");
Console.WriteLine($"Duplicate reactions: {mechanism.Reactions.Count(item => item.IsDuplicate)}");
Console.WriteLine($"Reactions with LOW: {mechanism.Reactions.Count(item => item.LowPressureLimit.Count > 0)}");
Console.WriteLine($"Reactions with TROE: {mechanism.Reactions.Count(item => item.TroeCentering.Count > 0)}");
Console.WriteLine($"Reactions with collider efficiencies: {mechanism.Reactions.Count(item => item.ColliderEfficiencies.Count > 0)}");
Console.WriteLine();
Console.WriteLine($"Selected element: {element}");
Console.WriteLine($"Species containing {element}: {markedSpecies.Count}");
Console.WriteLine($"Reactions mentioning those species: {affectedReactions.Count}");
Console.WriteLine($"Manual review rows: {reviewRows.Count(item => item.ReviewStatus == "ManualReview")}");
Console.WriteLine($"Auto candidate rows: {reviewRows.Count(item => item.ReviewStatus == "AutoCandidate")}");
Console.WriteLine($"Branch probability required: {reviewRows.Count(item => item.BranchProbabilityRequirement == nameof(BranchProbabilityRequirement.Required))}");
Console.WriteLine($"Complete marked-reaction expansions: {reviewRows.Count(item => item.ReactionTypeExpansionComplete)}");
Console.WriteLine($"Rows missing species trace rules: {reviewRows.Count(item => item.MissingSpeciesRules.Length > 0)}");
Console.WriteLine($"Rewrite plan candidates: {rewritePlanRows.Count}");
Console.WriteLine($"Rewrite plan ready: {rewritePlanRows.Count(item => item.PlanStatus == "Ready")}");
Console.WriteLine($"Rewrite plan needing selection: {rewritePlanRows.Count(item => item.PlanStatus == "NeedsSelection")}");
Console.WriteLine($"Rewrite plan needing REV parameters: {rewritePlanRows.Count(item => item.PlanStatus == "NeedsRevParameters")}");
Console.WriteLine();
Console.WriteLine("First species:");
foreach (var species in markedSpecies.Take(12))
{
    var count = species.ElementCounts.TryGetValue(element, out var value) ? value : 0;
    Console.WriteLine($"  {species.Index,3}. {species.Name,-10} {element}={count}");
}

Console.WriteLine();
Console.WriteLine("First affected reactions:");
foreach (var reaction in affectedReactions.Take(12))
{
    var flags = new List<string>();
    if (reaction.IsDuplicate) flags.Add("DUP");
    if (reaction.LowPressureLimit.Count > 0) flags.Add("LOW");
    if (reaction.TroeCentering.Count > 0) flags.Add("TROE");
    if (reaction.ColliderEfficiencies.Count > 0) flags.Add("EFF");

    var suffix = flags.Count == 0 ? string.Empty : $" [{string.Join(",", flags)}]";
    Console.WriteLine($"  {reaction.Index,3}. {reaction.Equation,-35} A={reaction.Rate.A:E3} b={reaction.Rate.B:0.###} Ea={reaction.Rate.Ea:0.###}{suffix}");
}

Console.WriteLine();
Console.WriteLine("Forward rewrite operation choices:");
foreach (var option in BranchRewriteOperationCatalog.ForwardOptions)
{
    Console.WriteLine($"  {option.Id,-32} {option.DisplayName} [{option.Formula}]");
}

Console.WriteLine();
Console.WriteLine("Reverse handling choices:");
foreach (var option in ReverseRewriteOperationCatalog.Options)
{
    var suffix = option.IsImplemented ? string.Empty : " [planned]";
    Console.WriteLine($"  {option.Id,-32} {option.DisplayName}{suffix}");
}

if (!string.IsNullOrWhiteSpace(reviewCsvPath))
{
    CsvWriter.WriteReactionReview(reviewCsvPath, reviewRows);
    Console.WriteLine();
    Console.WriteLine($"Review CSV written: {Path.GetFullPath(reviewCsvPath)}");
}

if (!string.IsNullOrWhiteSpace(rewritePlanCsvPath))
{
    CsvWriter.WriteReactionRewritePlan(rewritePlanCsvPath, rewritePlanRows);
    Console.WriteLine();
    Console.WriteLine($"Rewrite plan CSV written: {Path.GetFullPath(rewritePlanCsvPath)}");
}

return 0;

static int ValidatePlan(string[] arguments)
{
    if (arguments.Length < 2)
    {
        Console.Error.WriteLine("Usage: validate-plan <rewrite-plan.csv> [normalized-plan.csv]");
        return 2;
    }

    var planPath = arguments[1];
    if (!File.Exists(planPath))
    {
        Console.Error.WriteLine($"File not found: {planPath}");
        return 2;
    }

    try
    {
        var rows = new ReactionRewritePlanCsvReader().Read(planPath);
        var result = new ReactionRewritePlanValidator().Validate(rows);
        var errors = result.Issues.Count(issue => issue.Severity == PlanValidationSeverity.Error);
        var warnings = result.Issues.Count(issue => issue.Severity == PlanValidationSeverity.Warning);

        Console.WriteLine("TrackMechReviseTool rewrite plan validation");
        Console.WriteLine($"Input: {Path.GetFullPath(planPath)}");
        Console.WriteLine($"Rows: {result.NormalizedRows.Count}");
        Console.WriteLine($"Selected rows: {result.NormalizedRows.Count(row => row.Selected)}");
        Console.WriteLine($"Ready rows: {result.NormalizedRows.Count(row => row.PlanStatus == "Ready")}");
        Console.WriteLine($"Errors: {errors}");
        Console.WriteLine($"Warnings: {warnings}");

        foreach (var issue in result.Issues.Take(30))
        {
            Console.WriteLine($"  {issue.Severity,-7} {issue.Code,-30} RXN {issue.SourceReactionIndex} {issue.Location}: {issue.Message}");
        }
        if (result.Issues.Count > 30)
        {
            Console.WriteLine($"  ... {result.Issues.Count - 30} additional issues");
        }

        if (arguments.Length > 2 && !string.IsNullOrWhiteSpace(arguments[2]))
        {
            CsvWriter.WriteReactionRewritePlan(arguments[2], result.NormalizedRows);
            Console.WriteLine($"Normalized plan written: {Path.GetFullPath(arguments[2])}");
        }

        return result.IsValid ? 0 : 1;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
    {
        Console.Error.WriteLine($"Plan validation failed: {exception.Message}");
        return 2;
    }
}
