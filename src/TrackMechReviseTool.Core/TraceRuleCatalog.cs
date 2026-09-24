using System.Globalization;
using System.Text;

namespace TrackMechReviseTool.Core;

public sealed record MarkedSpeciesState(string Name, int MarkedAtomCount);

public sealed record SpeciesTraceRule(string SourceSpecies, IReadOnlyList<MarkedSpeciesState> States)
{
    public IReadOnlyList<string> MarkedSpecies => States.Select(item => item.Name).ToArray();
}

public sealed class TraceRuleCatalog
{
    public IReadOnlyList<SpeciesTraceRule> GetRules(OutMechanism mechanism, string elementSymbol)
    {
        ArgumentNullException.ThrowIfNull(mechanism);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementSymbol);

        var elementSymbols = mechanism.Elements
            .Select(item => item.Symbol)
            .OrderByDescending(item => item.Length)
            .ThenBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return mechanism.Species
            .Where(species => species.ContainsElement(elementSymbol))
            .Select(species => BuildRule(species, elementSymbol, elementSymbols))
            .Where(rule => rule is not null)
            .Cast<SpeciesTraceRule>()
            .ToArray();
    }

    private static SpeciesTraceRule? BuildRule(
        SpeciesInfo species,
        string elementSymbol,
        IReadOnlyList<string> elementSymbols)
    {
        if (!species.ElementCounts.TryGetValue(elementSymbol, out var expectedAtomCount) ||
            expectedAtomCount <= 0)
        {
            return null;
        }

        var segments = ParseFormula(species.Name, elementSymbols);
        var targetSegments = segments
            .Select((segment, index) => new { Segment = segment, Index = index })
            .Where(item => item.Segment.IsElement &&
                           string.Equals(item.Segment.Symbol, elementSymbol, StringComparison.OrdinalIgnoreCase))
            .Select(item => new TargetFormulaSegment(item.Index, item.Segment))
            .ToArray();
        if (targetSegments.Sum(item => item.Segment.Count) != expectedAtomCount)
        {
            return null;
        }

        var states = new List<MarkedSpeciesState>();
        var seen = new HashSet<(string Name, int MarkedAtomCount)>();
        var allocations = new int[targetSegments.Length];

        for (var markedAtomCount = 1; markedAtomCount <= expectedAtomCount; markedAtomCount++)
        {
            EnumerateAllocations(
                targetSegments,
                0,
                markedAtomCount,
                allocations,
                allocation =>
                {
                    var name = RenderMarkedName(segments, targetSegments, allocation);
                    if (seen.Add((name, markedAtomCount)))
                    {
                        states.Add(new MarkedSpeciesState(name, markedAtomCount));
                    }
                });
        }

        return states.Count == 0 ? null : new SpeciesTraceRule(species.Name, states);
    }

    private static IReadOnlyList<FormulaSegment> ParseFormula(
        string speciesName,
        IReadOnlyList<string> elementSymbols)
    {
        var segments = new List<FormulaSegment>();
        var position = 0;

        while (position < speciesName.Length)
        {
            var symbol = elementSymbols.FirstOrDefault(candidate =>
                position + candidate.Length <= speciesName.Length &&
                speciesName.AsSpan(position, candidate.Length)
                    .Equals(candidate.AsSpan(), StringComparison.OrdinalIgnoreCase));
            if (symbol is null)
            {
                segments.Add(new FormulaSegment(speciesName[position].ToString(), string.Empty, 0, false));
                position++;
                continue;
            }

            var originalSymbol = speciesName.Substring(position, symbol.Length);
            position += symbol.Length;
            var numberStart = position;
            while (position < speciesName.Length && char.IsDigit(speciesName[position]))
            {
                position++;
            }

            var countText = speciesName[numberStart..position];
            var count = countText.Length == 0
                ? 1
                : int.Parse(countText, NumberStyles.None, CultureInfo.InvariantCulture);
            segments.Add(new FormulaSegment(originalSymbol + countText, originalSymbol, count, true));
        }

        return segments;
    }

    private static void EnumerateAllocations(
        IReadOnlyList<TargetFormulaSegment> targetSegments,
        int targetIndex,
        int remaining,
        int[] allocations,
        Action<int[]> addState)
    {
        if (targetIndex == targetSegments.Count)
        {
            if (remaining == 0)
            {
                addState((int[])allocations.Clone());
            }
            return;
        }

        var capacity = targetSegments[targetIndex].Segment.Count;
        for (var marked = Math.Min(capacity, remaining); marked >= 0; marked--)
        {
            allocations[targetIndex] = marked;
            EnumerateAllocations(targetSegments, targetIndex + 1, remaining - marked, allocations, addState);
        }
    }

    private static string RenderMarkedName(
        IReadOnlyList<FormulaSegment> segments,
        IReadOnlyList<TargetFormulaSegment> targetSegments,
        IReadOnlyList<int> allocations)
    {
        var allocationBySegment = new Dictionary<int, int>();
        for (var index = 0; index < targetSegments.Count; index++)
        {
            allocationBySegment[targetSegments[index].Index] = allocations[index];
        }

        var builder = new StringBuilder();
        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            if (!allocationBySegment.TryGetValue(index, out var markedCount) || markedCount == 0)
            {
                builder.Append(segment.Text);
                continue;
            }

            var unmarkedCount = segment.Count - markedCount;
            if (unmarkedCount == 0)
            {
                builder.Append(segment.Symbol).Append('*');
                if (markedCount > 1)
                {
                    builder.Append(markedCount.ToString(CultureInfo.InvariantCulture));
                }
                continue;
            }

            for (var atom = 0; atom < unmarkedCount; atom++)
            {
                builder.Append(segment.Symbol);
            }
            for (var atom = 0; atom < markedCount; atom++)
            {
                builder.Append(segment.Symbol).Append('*');
            }
        }

        return builder.ToString();
    }

    private sealed record FormulaSegment(string Text, string Symbol, int Count, bool IsElement);

    private sealed record TargetFormulaSegment(int Index, FormulaSegment Segment);
}
