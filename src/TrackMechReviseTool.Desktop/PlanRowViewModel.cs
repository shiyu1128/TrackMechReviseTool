using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using TrackMechReviseTool.Core;

namespace TrackMechReviseTool.Desktop;

public sealed record ProbabilityOption(double? Value, string Display);

public sealed class PlanRowViewModel : INotifyPropertyChanged
{
    private readonly ReactionRewritePlanRow source;
    private bool selected;
    private double? forwardProbability;
    private double? reverseProbability;
    private double? revA;
    private double? revN;
    private double? revE;

    public PlanRowViewModel(
        ReactionRewritePlanRow source,
        bool isOriginal = false,
        IReadOnlyList<ProbabilityOption>? forwardProbabilityOptions = null,
        IReadOnlyList<ProbabilityOption>? reverseProbabilityOptions = null)
    {
        this.source = source;
        IsOriginal = isOriginal;
        CanEditReverseProbability = !isOriginal &&
            !string.Equals(source.ExplicitRevRequirement, "REV_NOT_APPLICABLE", StringComparison.Ordinal);
        ForwardProbabilityOptions = forwardProbabilityOptions ?? [];
        ReverseProbabilityOptions = CanEditReverseProbability
            ? reverseProbabilityOptions ?? []
            : [];
        selected = !isOriginal && source.Selected;
        forwardProbability = isOriginal
            ? null
            : MatchOptionValue(source.ForwardProbability, ForwardProbabilityOptions);
        reverseProbability = CanEditReverseProbability
            ? MatchOptionValue(source.ReverseProbability, ReverseProbabilityOptions)
            : null;
        revA = isOriginal ? null : source.RevA;
        revN = isOriginal ? null : source.RevN;
        revE = isOriginal ? null : source.RevE;
    }

    public bool IsOriginal { get; }
    public bool CanEditReverseProbability { get; }
    public int SourceReactionIndex => source.SourceReactionIndex;
    public string SourceEquation => source.SourceEquation;
    public string SequenceLabel => $"{SourceReactionIndex}{AlphabeticIndex(IsOriginal ? 1 : source.CandidateIndex + 1)}";
    public string RowKind => IsOriginal ? "原反应" : "标记反应";
    public int? MarkedAtomCount => IsOriginal ? null : source.MarkedAtomCount;
    public string DisplayEquation => IsOriginal ? source.SourceEquation : source.CandidateEquation;
    public int? Gf => IsOriginal ? null : source.ForwardAssignmentMultiplicity;
    public int? Gr => IsOriginal ? null : source.ReverseAssignmentMultiplicity;
    public string ExplicitRevRequirement => IsOriginal ? string.Empty : source.ExplicitRevRequirement;
    public string RevInputPrompt => IsOriginal ? string.Empty : source.RevInputPrompt;
    public string PlanStatus => IsOriginal ? "Original" : source.PlanStatus;
    public string ValidationMessage => IsOriginal ? "保留原始反应，不参与标记分支编辑" : source.ValidationMessage;
    public IReadOnlyList<ProbabilityOption> ForwardProbabilityOptions { get; }
    public IReadOnlyList<ProbabilityOption> ReverseProbabilityOptions { get; }
    public string ForwardProbabilityHint => IsOriginal
        ? string.Empty
        : ProbabilityHint(source.ForwardBranchCount, "gf");
    public string ReverseProbabilityHint => IsOriginal
        ? string.Empty
        : CanEditReverseProbability
            ? ProbabilityHint(source.ReverseBranchCount, "gr")
            : "不可逆反应不使用逆向概率";

    public bool Selected
    {
        get => selected;
        set
        {
            if (!IsOriginal) selected = value;
        }
    }

    public double? ForwardProbability
    {
        get => forwardProbability;
        set
        {
            if (!IsOriginal && !Nullable.Equals(forwardProbability, value))
            {
                forwardProbability = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ForwardProbabilityText));
            }
        }
    }

    public string ForwardProbabilityText
    {
        get => ProbabilityFraction.Format(ForwardProbability);
        set => ForwardProbability = ProbabilityFraction.Parse(value);
    }

    public double? ReverseProbability
    {
        get => reverseProbability;
        set
        {
            if (CanEditReverseProbability && !Nullable.Equals(reverseProbability, value))
            {
                reverseProbability = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReverseProbabilityText));
            }
        }
    }

    public string ReverseProbabilityText
    {
        get => ProbabilityFraction.Format(ReverseProbability);
        set => ReverseProbability = ProbabilityFraction.Parse(value);
    }

    public double? RevA
    {
        get => revA;
        set
        {
            if (!IsOriginal) revA = value;
        }
    }

    public double? RevN
    {
        get => revN;
        set
        {
            if (!IsOriginal) revN = value;
        }
    }

    public double? RevE
    {
        get => revE;
        set
        {
            if (!IsOriginal) revE = value;
        }
    }

    public ReactionRewritePlanRow ToCoreRow()
    {
        if (IsOriginal)
        {
            throw new InvalidOperationException("The original display row is not a rewrite-plan candidate.");
        }

        return source with
        {
            Selected = Selected,
            ForwardProbability = ForwardProbability,
            ReverseProbability = ReverseProbability,
            RevA = RevA,
            RevN = RevN,
            RevE = RevE
        };
    }

    private static string AlphabeticIndex(int index)
    {
        var result = string.Empty;
        while (index > 0)
        {
            index--;
            result = (char)('a' + index % 26) + result;
            index /= 26;
        }

        return result;
    }

    private static string ProbabilityHint(int branchCount, string multiplicityName)
    {
        return branchCount == 1
            ? "唯一分支，规则值为 1/1；仍可手动输入分数"
            : $"{branchCount} 个竞争分支：提供约分后的等分值及按 {multiplicityName} 归一化的统计分数；仍可手动输入分数";
    }

    private static double? MatchOptionValue(double? value, IReadOnlyList<ProbabilityOption> options)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return options
            .Where(option => option.Value.HasValue)
            .Select(option => option.Value!.Value)
            .FirstOrDefault(optionValue => Math.Abs(optionValue - value.Value) <= 1e-12, value.Value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public static class ProbabilityFraction
{
    private const int MaximumDenominator = 10000;
    private const double ApproximationTolerance = 1e-12;

    public static string Format(double? value)
    {
        if (!value.HasValue)
        {
            return string.Empty;
        }
        if (!double.IsFinite(value.Value))
        {
            return value.Value.ToString(CultureInfo.InvariantCulture);
        }

        var (numerator, denominator) = Approximate(value.Value);
        return $"{numerator.ToString(CultureInfo.InvariantCulture)}/{denominator.ToString(CultureInfo.InvariantCulture)}";
    }

    public static string Format(long numerator, long denominator)
    {
        if (denominator == 0)
        {
            throw new DivideByZeroException("Probability denominator cannot be zero.");
        }
        if (denominator < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        var divisor = GreatestCommonDivisor(Math.Abs(numerator), denominator);
        return $"{(numerator / divisor).ToString(CultureInfo.InvariantCulture)}/{(denominator / divisor).ToString(CultureInfo.InvariantCulture)}";
    }

    public static double? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        var slashIndex = trimmed.IndexOf('/');
        if (slashIndex >= 0)
        {
            if (trimmed.IndexOf('/', slashIndex + 1) >= 0 ||
                !long.TryParse(trimmed[..slashIndex].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numerator) ||
                !long.TryParse(trimmed[(slashIndex + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var denominator) ||
                denominator == 0)
            {
                throw new FormatException("概率必须是分数，例如 1/2、1/3 或 1/1。");
            }

            return numerator / (double)denominator;
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericValue))
        {
            return numericValue;
        }

        throw new FormatException("概率必须是分数，例如 1/2、1/3 或 1/1。");
    }

    private static (long Numerator, long Denominator) Approximate(double value)
    {
        var sign = Math.Sign(value);
        var target = Math.Abs(value);
        if (target == 0)
        {
            return (0, 1);
        }

        long previousNumerator = 0;
        long numerator = 1;
        long previousDenominator = 1;
        long denominator = 0;
        var remainder = target;

        for (var iteration = 0; iteration < 32; iteration++)
        {
            var whole = (long)Math.Floor(remainder);
            var nextNumerator = checked(whole * numerator + previousNumerator);
            var nextDenominator = checked(whole * denominator + previousDenominator);
            if (nextDenominator > MaximumDenominator)
            {
                break;
            }

            previousNumerator = numerator;
            numerator = nextNumerator;
            previousDenominator = denominator;
            denominator = nextDenominator;

            if (Math.Abs(target - numerator / (double)denominator) <= ApproximationTolerance)
            {
                break;
            }

            var fractional = remainder - whole;
            if (fractional <= double.Epsilon)
            {
                break;
            }
            remainder = 1.0 / fractional;
        }

        return (sign * numerator, denominator == 0 ? 1 : denominator);
    }

    private static long GreatestCommonDivisor(long left, long right)
    {
        while (right != 0)
        {
            (left, right) = (right, left % right);
        }

        return left == 0 ? 1 : left;
    }
}
