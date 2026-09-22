using TrackMechReviseTool.Core;

namespace TrackMechReviseTool.Desktop;

public sealed class PlanRowViewModel
{
    private readonly ReactionRewritePlanRow source;

    public PlanRowViewModel(ReactionRewritePlanRow source)
    {
        this.source = source;
        Selected = source.Selected;
        ForwardProbability = source.ForwardProbability;
        ReverseProbability = source.ReverseProbability;
        RevA = source.RevA;
        RevN = source.RevN;
        RevE = source.RevE;
    }

    public int SourceReactionIndex => source.SourceReactionIndex;
    public string SourceEquation => source.SourceEquation;
    public int CandidateIndex => source.CandidateIndex;
    public int MarkedAtomCount => source.MarkedAtomCount;
    public string CandidateEquation => source.CandidateEquation;
    public int Gf => source.ForwardAssignmentMultiplicity;
    public int Gr => source.ReverseAssignmentMultiplicity;
    public string ExplicitRevRequirement => source.ExplicitRevRequirement;
    public string RevInputPrompt => source.RevInputPrompt;
    public string PlanStatus => source.PlanStatus;
    public string ValidationMessage => source.ValidationMessage;
    public bool Selected { get; set; }
    public double? ForwardProbability { get; set; }
    public double? ReverseProbability { get; set; }
    public double? RevA { get; set; }
    public double? RevN { get; set; }
    public double? RevE { get; set; }

    public ReactionRewritePlanRow ToCoreRow()
    {
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
}
