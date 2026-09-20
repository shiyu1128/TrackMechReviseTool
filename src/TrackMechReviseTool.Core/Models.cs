namespace TrackMechReviseTool.Core;

public sealed record ChemicalElement(string Symbol, double AtomicWeight);

public sealed record SpeciesInfo(
    int Index,
    string Name,
    string Phase,
    int Charge,
    double MolecularWeight,
    double LowTemperature,
    double HighTemperature,
    IReadOnlyDictionary<string, int> ElementCounts)
{
    public bool ContainsElement(string elementSymbol)
    {
        return ElementCounts.TryGetValue(elementSymbol, out var count) && count > 0;
    }
}

public sealed record ArrheniusRate(double A, double B, double Ea);

public sealed class ReactionInfo
{
    public required int Index { get; init; }
    public required string Equation { get; init; }
    public required ArrheniusRate Rate { get; init; }
    public bool IsDuplicate { get; set; }
    public List<string> Warnings { get; } = [];
    public List<double> LowPressureLimit { get; } = [];
    public List<double> TroeCentering { get; } = [];
    public Dictionary<string, double> ColliderEfficiencies { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Notes { get; } = [];
}

public sealed class OutMechanism
{
    public List<ChemicalElement> Elements { get; } = [];
    public List<SpeciesInfo> Species { get; } = [];
    public List<ReactionInfo> Reactions { get; } = [];

    public IReadOnlyList<string> MarkableElements()
    {
        return Elements
            .Select(element => element.Symbol)
            .Where(symbol => !string.Equals(symbol, "AR", StringComparison.OrdinalIgnoreCase))
            .Where(symbol => !string.Equals(symbol, "HE", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
