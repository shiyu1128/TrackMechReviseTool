namespace TrackMechReviseTool.Core;

public enum BranchProbabilityRequirement
{
    NotApplicable,
    NotRequired,
    Required,
    AtomBalanceError
}

public sealed record RewriteOperationOption(
    string Id,
    string DisplayName,
    string Formula,
    string Description,
    IReadOnlyList<string> RequiredInputs,
    bool IsImplemented = true);

public static class BranchRewriteOperationCatalog
{
    public const string DirectCopy = "DIRECT_COPY";
    public const string DeterministicAtomMap = "DETERMINISTIC_ATOM_MAP";
    public const string FullLabeledStateExpansion = "FULL_LABELED_STATE_EXPANSION";
    public const string EqualProbabilitySplit = "EQUAL_PROBABILITY_SPLIT";
    public const string StatisticalMultiplicitySplit = "STATISTICAL_MULTIPLICITY_SPLIT";
    public const string CustomProbabilitySplit = "CUSTOM_PROBABILITY_SPLIT";
    public const string SymmetryDegeneracyScaling = "SYMMETRY_DEGENERACY_SCALING";
    public const string BondConstrainedMapping = "BOND_CONSTRAINED_MAPPING";
    public const string ManualBranches = "MANUAL_BRANCHES";

    public static IReadOnlyList<RewriteOperationOption> ForwardOptions { get; } =
    [
        new(
            DirectCopy,
            "直接传递并复制速率",
            "k_i = k_original",
            "只有一个可追踪原子且去向唯一。生成一条标记反应并复制 A、b、Ea。",
            []),
        new(
            DeterministicAtomMap,
            "指定原子去向",
            "k_i = k_original",
            "有多个可追踪原子，但每个标记原子的去向可由原子映射唯一确定。",
            ["反应物标记位点到产物标记位点的映射"]),
        new(
            FullLabeledStateExpansion,
            "完整标记态展开",
            "k(state_i) = k_original",
            "为每一种不同的标记反应物状态生成对应产物状态；不同输入状态不是同一输入态的竞争分支。",
            ["每个输入标记态对应的产物标记态"]),
        new(
            EqualProbabilitySplit,
            "等概率分支",
            "p_i = 1/n; A_i = p_i A_original",
            "同一标记输入态有 n 个等价产物去向，b 和 Ea 不变，所有正向分支概率之和必须为 1。",
            ["允许的分支集合"]),
        new(
            StatisticalMultiplicitySplit,
            "按简并度分配",
            "p_i = g_i/sum(g); A_i = p_i A_original",
            "按等价位点数或通道简并度分配正向速率，仅在化学键断裂机理允许这些通道时使用。",
            ["每个分支的简并度 g_i", "允许的分支集合"]),
        new(
            CustomProbabilitySplit,
            "自定义概率分配",
            "A_i = p_i A_original; sum(p_i) = 1",
            "用户为同一输入标记态逐支输入概率。程序校验概率非负且总和为 1。",
            ["每个正向分支的概率 p_i"]),
        new(
            SymmetryDegeneracyScaling,
            "对称性简并倍数",
            "A_i = g_i A_original",
            "交叉标记反应物代表多个不可区分排列时按简并度放大速率，例如 O+O* 的 g=2。",
            ["反应物状态简并度 g_i", "产物原子映射"]),
        new(
            BondConstrainedMapping,
            "按化学键结构限定",
            "仅保留允许通道，再对保留通道应用复制或概率分配",
            "用于 HONO 等不能仅按统计概率处理的反应。先排除违反键来源或断键路径的分支。",
            ["允许/禁止的原子映射", "保留分支的速率分配方式"]),
        new(
            ManualBranches,
            "完全手工定义",
            "用户逐条填写反应与 A、b、Ea",
            "保留给现有规则无法可靠覆盖的反应。程序仍检查元素守恒与分支速率和。",
            ["标记反应式", "各分支 A、b、Ea"])
    ];
}

public static class ReverseRewriteOperationCatalog
{
    public const string NotApplicable = "REVERSE_NOT_APPLICABLE";
    public const string ImplicitThermodynamic = "REVERSE_IMPLICIT_THERMO";
    public const string MirrorForwardProbabilities = "REVERSE_MIRROR_FORWARD";
    public const string IndependentProbabilities = "REVERSE_INDEPENDENT_PROBABILITIES";
    public const string ScaleOriginalReverse = "REVERSE_SCALE_ORIGINAL";
    public const string ManualRevArrhenius = "REVERSE_MANUAL_REV";
    public const string ExplicitReverseReactions = "REVERSE_EXPLICIT_REACTIONS";
    public const string CalculateFromThermo = "REVERSE_CALCULATE_FROM_THERMO";

    public static IReadOnlyList<RewriteOperationOption> Options { get; } =
    [
        new(
            NotApplicable,
            "无逆向处理",
            "irreversible",
            "原反应使用 =>，不生成逆向通道。",
            []),
        new(
            ImplicitThermodynamic,
            "保持可逆并由热力学隐式求逆",
            "k_r = k_f/K_c",
            "仅适合不需要独立逆向概率控制的标记反应。",
            []),
        new(
            MirrorForwardProbabilities,
            "逆向沿用正向概率",
            "q_i = p_i",
            "正逆原子去向的概率完全一致时使用。",
            ["确认正逆概率一致"]),
        new(
            IndependentProbabilities,
            "单独设置逆向概率",
            "sum(q_j) = 1",
            "正逆概率不一致时，为逆向通道单独填写 q_j。",
            ["每个逆向分支的概率 q_j"]),
        new(
            ScaleOriginalReverse,
            "按倍数缩放并显式写 REV",
            "k_r* = f_r k_r,original",
            "适用于规则表中的 0.5、1、2 倍逆向速率及其他已知简并因子。正向与逆向总倍率不同时必须以 REV 写出，不能使用隐式逆向速率。",
            ["逆向倍数 f_r", "原逆反应 Arrhenius 参数"]),
        new(
            ManualRevArrhenius,
            "手工填写 REV 参数",
            "REV / A_r b_r Ea_r /",
            "直接填写每条标记反应的逆向 Arrhenius 参数。",
            ["A_r", "b_r", "Ea_r"]),
        new(
            ExplicitReverseReactions,
            "拆成显式正反反应",
            "forward => products; products => reactants",
            "将可逆反应拆成不可逆正向和不可逆逆向，适合 duplicate 或分支集合不对称的情况。",
            ["每条逆向反应式", "每条逆向 A、b、Ea"]),
        new(
            CalculateFromThermo,
            "由热力学计算并拟合逆向速率",
            "k_r(T) = k_f(T)/K_c(T)",
            "在温区内计算 k_r(T) 后拟合 Arrhenius 参数；当前阶段仅作为后续功能入口。",
            ["拟合温区", "温度采样点", "热力学数据"],
            IsImplemented: false)
    ];
}

public sealed record BranchProbabilityAssessment(
    BranchProbabilityRequirement Requirement,
    int ReactantElementAtoms,
    int ProductElementAtoms,
    string RecommendedForwardOperationId,
    string RecommendedReverseOperationId,
    IReadOnlyList<string> ForwardOperationIds,
    IReadOnlyList<string> ReverseOperationIds,
    IReadOnlyList<string> RequiredUserInputs,
    IReadOnlyList<string> Notes);

public sealed class BranchProbabilityAssessmentService
{
    private readonly ReactionEquationParser equationParser = new();

    public BranchProbabilityAssessment Assess(
        OutMechanism mechanism,
        ReactionInfo reaction,
        string elementSymbol,
        IReadOnlyDictionary<string, SpeciesTraceRule> ruleMap)
    {
        var equation = equationParser.Parse(reaction.Equation);
        var speciesByName = mechanism.Species.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var reactantAtoms = CountElementAtoms(equation.Reactants, speciesByName, elementSymbol);
        var productAtoms = CountElementAtoms(equation.Products, speciesByName, elementSymbol);
        var allParticipants = equation.Reactants.Concat(equation.Products).ToArray();
        var unknownSpecies = allParticipants
            .Where(item => !speciesByName.ContainsKey(item.SpeciesName))
            .Select(item => item.SpeciesName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToArray();

        var traceableParticipants = allParticipants
            .Where(item => ruleMap.ContainsKey(item.SpeciesName))
            .ToArray();
        var hasPositionalVariants = traceableParticipants
            .Any(item => ruleMap[item.SpeciesName].MarkedSpecies.Count > 1);
        var hasRepeatedTraceableReactants = HasRepeatedTraceableSpecies(equation.Reactants, ruleMap);
        var hasRepeatedTraceableProducts = HasRepeatedTraceableSpecies(equation.Products, ruleMap);

        var requirement = reactantAtoms == 0 && productAtoms == 0
            ? BranchProbabilityRequirement.NotApplicable
            : reactantAtoms != productAtoms
                ? BranchProbabilityRequirement.AtomBalanceError
                : reactantAtoms <= 1 && !hasPositionalVariants
                    ? BranchProbabilityRequirement.NotRequired
                    : BranchProbabilityRequirement.Required;

        var forwardOptions = BuildForwardOptions(
            requirement,
            reactantAtoms,
            hasPositionalVariants,
            hasRepeatedTraceableReactants || hasRepeatedTraceableProducts);
        var recommended = RecommendForwardOperation(
            requirement,
            equation,
            reactantAtoms,
            hasPositionalVariants,
            hasRepeatedTraceableReactants,
            hasRepeatedTraceableProducts,
            ruleMap);
        var reverseOptions = BuildReverseOptions(equation.ArrowKind, requirement);
        var recommendedReverse = RecommendReverseOperation(
            equation.ArrowKind,
            requirement,
            hasRepeatedTraceableReactants,
            hasRepeatedTraceableProducts,
            hasPositionalVariants);
        var requiredInputs = BuildRequiredInputs(requirement, equation.ArrowKind);
        var notes = new List<string>();

        if (unknownSpecies.Length > 0)
        {
            notes.Add($"species not found in .out table: {string.Join(", ", unknownSpecies)}");
        }
        if (requirement == BranchProbabilityRequirement.AtomBalanceError)
        {
            notes.Add("target-element atom count is not balanced; automatic rewrite must stop");
        }
        if (hasPositionalVariants)
        {
            notes.Add("contains species with multiple labeled positions");
        }
        if (hasRepeatedTraceableReactants)
        {
            notes.Add("contains repeated identical traceable reactants; check forward symmetry degeneracy");
        }
        if (hasRepeatedTraceableProducts)
        {
            notes.Add("contains repeated identical traceable products; check reverse symmetry degeneracy");
        }
        if (equation.ArrowKind == ReactionArrowKind.Reversible && requirement == BranchProbabilityRequirement.Required)
        {
            notes.Add("forward and reverse branch probabilities must be confirmed separately");
        }

        return new BranchProbabilityAssessment(
            requirement,
            reactantAtoms,
            productAtoms,
            recommended,
            recommendedReverse,
            forwardOptions,
            reverseOptions,
            requiredInputs,
            notes);
    }

    private static bool HasRepeatedTraceableSpecies(
        IEnumerable<ReactionParticipant> participants,
        IReadOnlyDictionary<string, SpeciesTraceRule> ruleMap)
    {
        return participants
            .Where(item => ruleMap.ContainsKey(item.SpeciesName))
            .GroupBy(item => item.SpeciesName, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Sum(item => item.StoichiometricCoefficient) > 1);
    }

    private static int CountElementAtoms(
        IEnumerable<ReactionParticipant> participants,
        IReadOnlyDictionary<string, SpeciesInfo> speciesByName,
        string elementSymbol)
    {
        return participants.Sum(participant =>
        {
            if (!speciesByName.TryGetValue(participant.SpeciesName, out var species) ||
                !species.ElementCounts.TryGetValue(elementSymbol, out var atoms))
            {
                return 0;
            }

            return participant.StoichiometricCoefficient * atoms;
        });
    }

    private static IReadOnlyList<string> BuildForwardOptions(
        BranchProbabilityRequirement requirement,
        int elementAtoms,
        bool hasPositionalVariants,
        bool hasRepeatedTraceableSpecies)
    {
        if (requirement == BranchProbabilityRequirement.NotApplicable)
        {
            return [];
        }

        if (requirement == BranchProbabilityRequirement.NotRequired)
        {
            return [BranchRewriteOperationCatalog.DirectCopy, BranchRewriteOperationCatalog.ManualBranches];
        }

        if (requirement == BranchProbabilityRequirement.AtomBalanceError)
        {
            return [BranchRewriteOperationCatalog.ManualBranches];
        }

        var options = new List<string>
        {
            BranchRewriteOperationCatalog.DeterministicAtomMap,
            BranchRewriteOperationCatalog.FullLabeledStateExpansion,
            BranchRewriteOperationCatalog.CustomProbabilitySplit,
            BranchRewriteOperationCatalog.BondConstrainedMapping,
            BranchRewriteOperationCatalog.ManualBranches
        };

        if (elementAtoms > 1 || hasPositionalVariants)
        {
            options.Insert(2, BranchRewriteOperationCatalog.EqualProbabilitySplit);
            options.Insert(3, BranchRewriteOperationCatalog.StatisticalMultiplicitySplit);
        }

        if (hasRepeatedTraceableSpecies || hasPositionalVariants)
        {
            options.Insert(4, BranchRewriteOperationCatalog.SymmetryDegeneracyScaling);
        }

        return options.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string RecommendForwardOperation(
        BranchProbabilityRequirement requirement,
        ParsedReactionEquation equation,
        int elementAtoms,
        bool hasPositionalVariants,
        bool hasRepeatedTraceableReactants,
        bool hasRepeatedTraceableProducts,
        IReadOnlyDictionary<string, SpeciesTraceRule> ruleMap)
    {
        if (requirement == BranchProbabilityRequirement.NotApplicable)
        {
            return string.Empty;
        }
        if (requirement == BranchProbabilityRequirement.NotRequired)
        {
            return BranchRewriteOperationCatalog.DirectCopy;
        }
        if (requirement == BranchProbabilityRequirement.AtomBalanceError)
        {
            return BranchRewriteOperationCatalog.ManualBranches;
        }
        if (hasRepeatedTraceableReactants)
        {
            return BranchRewriteOperationCatalog.SymmetryDegeneracyScaling;
        }

        var reactantTraceableCount = equation.Reactants.Count(item => ruleMap.ContainsKey(item.SpeciesName));
        var productTraceableCount = equation.Products.Count(item => ruleMap.ContainsKey(item.SpeciesName));
        if (hasRepeatedTraceableProducts)
        {
            return BranchRewriteOperationCatalog.FullLabeledStateExpansion;
        }
        if (hasPositionalVariants && reactantTraceableCount > 1 && productTraceableCount > 1)
        {
            return BranchRewriteOperationCatalog.BondConstrainedMapping;
        }
        if (hasPositionalVariants && reactantTraceableCount == 1 && productTraceableCount > 1)
        {
            return BranchRewriteOperationCatalog.EqualProbabilitySplit;
        }
        if (elementAtoms > 1)
        {
            return reactantTraceableCount == productTraceableCount && !hasPositionalVariants
                ? BranchRewriteOperationCatalog.DeterministicAtomMap
                : BranchRewriteOperationCatalog.FullLabeledStateExpansion;
        }

        return BranchRewriteOperationCatalog.DeterministicAtomMap;
    }

    private static string RecommendReverseOperation(
        ReactionArrowKind arrowKind,
        BranchProbabilityRequirement requirement,
        bool hasRepeatedTraceableReactants,
        bool hasRepeatedTraceableProducts,
        bool hasPositionalVariants)
    {
        if (arrowKind == ReactionArrowKind.Irreversible)
        {
            return ReverseRewriteOperationCatalog.NotApplicable;
        }
        if (requirement == BranchProbabilityRequirement.NotRequired)
        {
            return ReverseRewriteOperationCatalog.ImplicitThermodynamic;
        }
        if (hasRepeatedTraceableReactants || hasRepeatedTraceableProducts)
        {
            return ReverseRewriteOperationCatalog.ScaleOriginalReverse;
        }
        if (hasPositionalVariants)
        {
            return ReverseRewriteOperationCatalog.IndependentProbabilities;
        }

        return ReverseRewriteOperationCatalog.MirrorForwardProbabilities;
    }

    private static IReadOnlyList<string> BuildReverseOptions(
        ReactionArrowKind arrowKind,
        BranchProbabilityRequirement requirement)
    {
        if (arrowKind == ReactionArrowKind.Irreversible)
        {
            return [ReverseRewriteOperationCatalog.NotApplicable];
        }

        if (requirement == BranchProbabilityRequirement.NotRequired)
        {
            return
            [
                ReverseRewriteOperationCatalog.ImplicitThermodynamic,
                ReverseRewriteOperationCatalog.ManualRevArrhenius,
                ReverseRewriteOperationCatalog.ExplicitReverseReactions
            ];
        }

        return
        [
            ReverseRewriteOperationCatalog.MirrorForwardProbabilities,
            ReverseRewriteOperationCatalog.IndependentProbabilities,
            ReverseRewriteOperationCatalog.ScaleOriginalReverse,
            ReverseRewriteOperationCatalog.ManualRevArrhenius,
            ReverseRewriteOperationCatalog.ExplicitReverseReactions,
            ReverseRewriteOperationCatalog.CalculateFromThermo
        ];
    }

    private static IReadOnlyList<string> BuildRequiredInputs(
        BranchProbabilityRequirement requirement,
        ReactionArrowKind arrowKind)
    {
        var inputs = new List<string>();
        if (requirement == BranchProbabilityRequirement.Required)
        {
            inputs.Add("forward branch operation");
            inputs.Add("allowed atom mappings/branches");
            inputs.Add("forward probabilities or degeneracy factors");
        }
        if (arrowKind == ReactionArrowKind.Reversible && requirement == BranchProbabilityRequirement.Required)
        {
            inputs.Add("reverse handling operation");
            inputs.Add("reverse probabilities, multiplier, or Arrhenius parameters");
        }

        return inputs;
    }
}
