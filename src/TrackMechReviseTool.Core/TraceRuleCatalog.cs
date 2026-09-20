namespace TrackMechReviseTool.Core;

public sealed record MarkedSpeciesState(string Name, int MarkedAtomCount);

public sealed record SpeciesTraceRule(string SourceSpecies, IReadOnlyList<MarkedSpeciesState> States)
{
    public IReadOnlyList<string> MarkedSpecies => States.Select(item => item.Name).ToArray();
}

public sealed class TraceRuleCatalog
{
    private readonly Dictionary<string, List<SpeciesTraceRule>> rulesByElement = new(StringComparer.OrdinalIgnoreCase)
    {
        ["O"] =
        [
            new("O", [new("O*", 1)]),
            new("OH", [new("O*H", 1)]),
            new("H2O", [new("H2O*", 1)]),
            new("HO2", [new("HOO*", 1), new("HO*2", 2)]),
            new("H2O2", [new("H2OO*", 1), new("H2O*2", 2)]),
            new("O2", [new("OO*", 1), new("O*2", 2)]),
            new("NO", [new("NO*", 1)]),
            new("N2O", [new("N2O*", 1)]),
            new("NO2", [new("NOO*", 1), new("NO*2", 2)]),
            new("HNO", [new("HNO*", 1)]),
            new("HON", [new("HO*N", 1)]),
            new("HONO", [new("HONO*", 1), new("HO*NO", 1), new("HO*NO*", 2)]),
            new("H2NO", [new("H2NO*", 1)]),
            new("HNOH", [new("HNO*H", 1)]),
            new("HNO2", [new("HNOO*", 1), new("HNO*2", 2)]),
            new("NO3", [new("NOOO*", 1), new("NOO*O*", 2), new("NO*3", 3)]),
            new("HNO3", [new("HNOOO*", 1), new("HNOO*O*", 2), new("HNO*3", 3)]),
            new("NH2OH", [new("NH2O*H", 1)])
        ]
    };

    public IReadOnlyList<SpeciesTraceRule> GetRules(string elementSymbol)
    {
        return rulesByElement.TryGetValue(elementSymbol, out var rules)
            ? rules
            : [];
    }
}
