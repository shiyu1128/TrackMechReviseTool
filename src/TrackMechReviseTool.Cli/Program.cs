using TrackMechReviseTool.Core;

if (args.Length > 0 && string.Equals(args[0], "validate-plan", StringComparison.OrdinalIgnoreCase))
{
    return ValidatePlan(args);
}
if (args.Length > 0 && string.Equals(args[0], "write-reactions", StringComparison.OrdinalIgnoreCase))
{
    return WriteReactions(args);
}
if (args.Length > 0 && string.Equals(args[0], "write-mechanism", StringComparison.OrdinalIgnoreCase))
{
    return WriteMechanism(args);
}
if (args.Length > 0 && string.Equals(args[0], "write-thermo", StringComparison.OrdinalIgnoreCase))
{
    return WriteThermo(args);
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

static int WriteReactions(string[] arguments)
{
    if (arguments.Length < 4)
    {
        Console.Error.WriteLine("Usage: write-reactions <mechanism.out> <rewrite-plan.csv> <output-reactions.inp>");
        return 2;
    }

    var outPath = arguments[1];
    var planPath = arguments[2];
    var outputPath = arguments[3];
    if (!File.Exists(outPath) || !File.Exists(planPath))
    {
        Console.Error.WriteLine(!File.Exists(outPath)
            ? $"File not found: {outPath}"
            : $"File not found: {planPath}");
        return 2;
    }

    try
    {
        var mechanism = new OutMechanismParser().Parse(outPath);
        var rows = new ReactionRewritePlanCsvReader().Read(planPath);
        var validationResult = new ReactionRewritePlanValidator().Validate(rows);
        if (!validationResult.IsValid)
        {
            Console.Error.WriteLine("Reaction output blocked because the rewrite plan contains validation errors.");
            foreach (var issue in validationResult.Issues
                         .Where(issue => issue.Severity == PlanValidationSeverity.Error)
                         .Take(30))
            {
                Console.Error.WriteLine(
                    $"  {issue.Code,-30} RXN {issue.SourceReactionIndex} {issue.Location}: {issue.Message}");
            }
            return 1;
        }

        new ChemkinReactionSectionWriter().Write(outputPath, mechanism, validationResult);
        Console.WriteLine("TrackMechReviseTool Chemkin reaction writer");
        Console.WriteLine($"Input .out: {Path.GetFullPath(outPath)}");
        Console.WriteLine($"Plan: {Path.GetFullPath(planPath)}");
        Console.WriteLine($"Written reactions: {validationResult.NormalizedRows.Count(row => row.Selected)}");
        Console.WriteLine($"Manual REV lines: {validationResult.NormalizedRows.Count(row => row.Selected && row.ExplicitRevRequirement == "REV_REQUIRED")}");
        Console.WriteLine($"Output: {Path.GetFullPath(outputPath)}");
        return 0;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
    {
        Console.Error.WriteLine($"Reaction writing failed: {exception.Message}");
        return 2;
    }
}

static int WriteMechanism(string[] arguments)
{
    if (arguments.Length < 4)
    {
        Console.Error.WriteLine("Usage: write-mechanism <mechanism.out> <rewrite-plan.csv> <output-mechanism.inp>");
        return 2;
    }

    var outPath = arguments[1];
    var planPath = arguments[2];
    var outputPath = arguments[3];
    if (!File.Exists(outPath) || !File.Exists(planPath))
    {
        Console.Error.WriteLine(!File.Exists(outPath)
            ? $"File not found: {outPath}"
            : $"File not found: {planPath}");
        return 2;
    }

    try
    {
        var mechanism = new OutMechanismParser().Parse(outPath);
        var rows = new ReactionRewritePlanCsvReader().Read(planPath);
        var validationResult = new ReactionRewritePlanValidator().Validate(rows);
        if (!validationResult.IsValid)
        {
            Console.Error.WriteLine("Mechanism output blocked because the rewrite plan contains validation errors.");
            foreach (var issue in validationResult.Issues
                         .Where(issue => issue.Severity == PlanValidationSeverity.Error)
                         .Take(30))
            {
                Console.Error.WriteLine(
                    $"  {issue.Code,-30} RXN {issue.SourceReactionIndex} {issue.Location}: {issue.Message}");
            }
            return 1;
        }

        new ChemkinMechanismWriter().Write(outputPath, mechanism, validationResult);
        Console.WriteLine("TrackMechReviseTool complete Chemkin mechanism writer");
        Console.WriteLine($"Input .out: {Path.GetFullPath(outPath)}");
        Console.WriteLine($"Plan: {Path.GetFullPath(planPath)}");
        Console.WriteLine($"Elements: {mechanism.Elements.Count}");
        Console.WriteLine($"Original species: {mechanism.Species.Count}");
        Console.WriteLine($"Original reactions: {mechanism.Reactions.Count}");
        Console.WriteLine($"Added marked reactions: {validationResult.NormalizedRows.Count(row => row.Selected)}");
        Console.WriteLine($"Output: {Path.GetFullPath(outputPath)}");
        return 0;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
    {
        Console.Error.WriteLine($"Mechanism writing failed: {exception.Message}");
        return 2;
    }
}

static int WriteThermo(string[] arguments)
{
    if (arguments.Length < 6)
    {
        Console.Error.WriteLine("Usage: write-thermo <thermo.dat> <mechanism.out> <rewrite-plan.csv> <element> <output-thermo.dat>");
        return 2;
    }

    var thermoPath = arguments[1];
    var outPath = arguments[2];
    var planPath = arguments[3];
    var element = arguments[4];
    var outputPath = arguments[5];
    foreach (var inputPath in new[] { thermoPath, outPath, planPath })
    {
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"File not found: {inputPath}");
            return 2;
        }
    }

    try
    {
        var thermo = new ThermoFileParser().Parse(thermoPath);
        var mechanism = new OutMechanismParser().Parse(outPath);
        if (!mechanism.MarkableElements().Contains(element, StringComparer.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Element '{element}' is not markable in the parsed .out mechanism.");
            return 2;
        }

        var rows = new ReactionRewritePlanCsvReader().Read(planPath);
        var validationResult = new ReactionRewritePlanValidator().Validate(rows);
        if (!validationResult.IsValid)
        {
            Console.Error.WriteLine("Thermodynamic output blocked because the rewrite plan contains validation errors.");
            foreach (var issue in validationResult.Issues
                         .Where(issue => issue.Severity == PlanValidationSeverity.Error)
                         .Take(30))
            {
                Console.Error.WriteLine(
                    $"  {issue.Code,-30} RXN {issue.SourceReactionIndex} {issue.Location}: {issue.Message}");
            }
            return 1;
        }

        var result = new MarkedThermoWriter().Write(
            outputPath,
            thermo,
            mechanism,
            validationResult,
            element);
        Console.WriteLine("TrackMechReviseTool marked thermodynamic writer");
        Console.WriteLine($"Input thermo: {Path.GetFullPath(thermoPath)}");
        Console.WriteLine($"Input .out: {Path.GetFullPath(outPath)}");
        Console.WriteLine($"Selected element: {element}");
        Console.WriteLine($"Source thermo entries: {result.SourceEntryCount}");
        Console.WriteLine($"Added thermo entries: {result.AddedSpecies.Count}");
        Console.WriteLine($"Added species: {string.Join(", ", result.AddedSpecies)}");
        Console.WriteLine($"Output: {Path.GetFullPath(outputPath)}");
        return 0;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
    {
        Console.Error.WriteLine($"Thermodynamic writing failed: {exception.Message}");
        return 2;
    }
}
