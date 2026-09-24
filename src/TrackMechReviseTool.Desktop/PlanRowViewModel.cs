using TrackMechReviseTool.Core;

namespace TrackMechReviseTool.Desktop;

public sealed class PlanRowViewModel
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
        IReadOnlyList<double?>? forwardProbabilityOptions = null,
        IReadOnlyList<double?>? reverseProbabilityOptions = null)
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
        forwardProbability = isOriginal ? null : source.ForwardProbability;
        reverseProbability = CanEditReverseProbability ? source.ReverseProbability : null;
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
    public IReadOnlyList<double?> ForwardProbabilityOptions { get; }
    public IReadOnlyList<double?> ReverseProbabilityOptions { get; }
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
            if (!IsOriginal) forwardProbability = value;
        }
    }

    public double? ReverseProbability
    {
        get => reverseProbability;
        set
        {
            if (CanEditReverseProbability) reverseProbability = value;
        }
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
            ? "唯一分支，规则值为 1；仍可手动输入"
            : $"{branchCount} 个竞争分支：提供等分值及按 {multiplicityName} 归一化的统计值；仍可手动输入";
    }
}
