# Branch probabilities required 改写规则

## 设计结论

程序把一次反应的选择拆成两个独立字段：

1. 正向标记分支操作：决定标记态如何展开，以及各正向分支如何分配原速率。
2. 逆向处理操作：仅对可逆反应使用，决定逆向概率是否与正向一致，以及是否需要 `REV` 或显式逆反应。

所有属于同一未标记输入态的竞争正向分支必须满足：

`sum(k_f,i) = k_f,original`

若仅缩放 Arrhenius 预指数因子，则：

`A_i = p_i A_original`，`sum(p_i) = 1`，并保持 `b_i = b_original`、`Ea_i = Ea_original`。

不同标记输入态不是同一输入态的竞争分支。对这些状态可以分别使用完整的原始速率常数。

## 按标记原子数生成反应类型

先统计原反应任意一侧目标元素的原子总数 `N`，然后建立 `k=1...N` 个标记数层级。每个新增反应的反应物和产物必须具有相同的 `k`。

对同种物种重复出现的情况，程序将等价排列合并成一个反应状态，并记录排列简并度。若该反应物状态只对应一个允许产物状态，则正向建议倍数等于反应物排列简并度；若对应多个产物状态，还需在这些竞争分支之间分配概率。

例如 `O+O+HE=O2+HE` 中 `N=2`：

| k | 合并后的新增反应类型 | 反应物排列简并度 | 正向处理 |
|---|---|---:|---|
| 1 | `O+O*+HE=OO*+HE` | 2 | `kf*=2kf`，逆向单独确认或填写 `REV` |
| 2 | `O*+O*+HE=O*2+HE` | 1 | `kf*=kf` |

因此该原反应新增 2 种带标记反应，而不是把两个单标记 O 的排列写成两条重复方程。

速率倍数的通式为 `kf,i = gR,i * p_i * kf,original`。其中 `gR,i` 是合并前的反应物等价排列数，`p_i` 是该反应物标记态到允许产物分支的概率。逆向对应为 `kr,i = gP,i * q_i * kr,original`。

程序直接采用 `.out` 中显示的 A、n、E 数值，不恢复 `.inp` 中更多的有效数字。例如 `.out` 为 `1.89E+13` 时，`2kf` 对应的 A 按 `3.78E+13` 处理。

对于可逆反应，Chemkin 在没有 `REV` 时会根据当前正向速率和热力学数据隐式计算逆向速率。因此必须比较期望正向总倍率 `gR*p_i` 与期望逆向总倍率 `gP*q_i`：二者相等时可继续使用隐式逆向速率；二者不相等时必须写成 `REV/A_r n_r E_r/`。示例的单标记反应为 `gf=2`、`gr=1`，所以正向 A 乘 2 后必须用 `REV` 显式恢复所需的原逆向速率；双标记反应为 `gf=1`、`gr=1`，在没有其他概率差异时不需要额外 `REV`。

若原子处于不等价化学位点，例如 HONO，不得只按组合数决定最终总数。程序先生成同标记数的候选组合，再由键结构规则或用户确认删除不允许的原子映射。因此审核表分别显示候选类型总数和计数状态：`Automatic` 表示候选数就是新增总数；`RequiresBranchOrAtomMapping` 表示最终总数要等用户勾选允许分支后确定；`IncompleteSpeciesRules` 表示必须先补齐物种标记规则。

## 正向操作选项

| ID | 界面名称 | 核心规则 | 需要用户确认或填写 |
|---|---|---|---|
| `DIRECT_COPY` | 直接传递并复制速率 | 唯一标记原子去向，`k*=k` | 无 |
| `DETERMINISTIC_ATOM_MAP` | 指定原子去向 | 多个原子但映射唯一，每个标记输入态 `k*=k` | 原子映射 |
| `FULL_LABELED_STATE_EXPANSION` | 完整标记态展开 | 枚举不同标记输入态，各状态 `k*=k` | 每个输入态对应的产物态 |
| `EQUAL_PROBABILITY_SPLIT` | 等概率分支 | 同一输入态的 n 个等价去向，`A_i=A/n` | 允许的分支集合 |
| `STATISTICAL_MULTIPLICITY_SPLIT` | 按简并度分配 | `p_i=g_i/sum(g)` | 每支简并度及允许分支 |
| `CUSTOM_PROBABILITY_SPLIT` | 自定义概率分配 | `A_i=p_i A` 且 `sum(p_i)=1` | 每支概率 |
| `SYMMETRY_DEGENERACY_SCALING` | 对称性简并倍数 | 输入标记态代表 g 个不可区分排列时，`A*=gA` | 简并度和原子映射 |
| `BOND_CONSTRAINED_MAPPING` | 按化学键结构限定 | 先排除不符合键来源和断键路径的通道，再复制或分配速率 | 允许/禁止映射及分配方式 |
| `MANUAL_BRANCHES` | 完全手工定义 | 用户逐条定义标记反应及参数 | 反应式和 A、b、Ea |

## 逆向操作选项

| ID | 界面名称 | 使用条件 |
|---|---|---|
| `REVERSE_NOT_APPLICABLE` | 无逆向处理 | 原反应为 `=>` |
| `REVERSE_IMPLICIT_THERMO` | 由热力学隐式求逆 | 无独立逆向概率要求 |
| `REVERSE_MIRROR_FORWARD` | 逆向沿用正向概率 | `q_i=p_i` |
| `REVERSE_INDEPENDENT_PROBABILITIES` | 单独设置逆向概率 | 正逆概率不一致，填写 `q_j` 且总和为 1 |
| `REVERSE_SCALE_ORIGINAL` | 按倍数缩放原逆向速率 | 已知 `k_r*=f_r k_r`，包括 0.5、1、2 倍 |
| `REVERSE_MANUAL_REV` | 手工填写 REV 参数 | 填写 `A_r`、`b_r`、`Ea_r` |
| `REVERSE_EXPLICIT_REACTIONS` | 拆成显式正反反应 | duplicate 或正逆分支集合不对称 |
| `REVERSE_CALCULATE_FROM_THERMO` | 热力学计算并拟合 | 后续功能；由 `k_r=k_f/Kc` 在温区内拟合 |

## 文档案例与操作映射

| 原案例 | 推荐正向操作 | 推荐逆向操作 | 关键点 |
|---|---|---|---|
| i-1 | `DIRECT_COPY` | 隐式求逆 | 单个 O 原子唯一传递，`k*=k` |
| i-2 | `DETERMINISTIC_ATOM_MAP` 或 `BOND_CONSTRAINED_MAPPING` | 隐式求逆 | 根据化学键决定 HNO 中 O 和外加 O 的去向 |
| ii-1 | `SYMMETRY_DEGENERACY_SCALING` | 手工 `REV` 或显式逆反应 | duplicate 必须逐通道保留；正逆简并度可能不同 |
| ii-2 | `SYMMETRY_DEGENERACY_SCALING` | `REVERSE_SCALE_ORIGINAL` | `O+O*` 正向为 `2kf`，逆向为 `kr` |
| iii-1 | `FULL_LABELED_STATE_EXPANSION` | 隐式或沿用正向 | 不同标记输入态各自使用完整 `kf` |
| iii-2 | `FULL_LABELED_STATE_EXPANSION` | `REVERSE_SCALE_ORIGINAL` | 正向 `kf`，逆向 `2kr` |
| iv-1 | `FULL_LABELED_STATE_EXPANSION` | `REVERSE_SCALE_ORIGINAL` | 正向 `kf`，特定逆向分支为 `0.5kr` |
| iv-2 | `EQUAL_PROBABILITY_SPLIT` | `REVERSE_SCALE_ORIGINAL` | 同一 `OO*` 输入态分成两个 `0.5kf`，各逆向为 `kr` |
| v | `BOND_CONSTRAINED_MAPPING` | 按确认后的通道处理 | HONO 的 O 来源由键结构决定，不能做纯组合统计 |

## 界面字段

每条需要审核的反应至少展示：原反应、目标元素原子数、可标记物种、正向操作、标记分支表、正向概率和、逆向操作、逆向参数、LOW/TROE/三体/duplicate 标志和确认状态。

保存前必须通过以下校验：元素守恒；每个输入标记态的竞争正向概率和为 1；独立逆向概率和为 1；duplicate 分组完整；压力依赖和三体附属数据已复制；需要 `REV` 的条目不允许留空。

## Rewrite plan 表

`rewrite_plan_O.csv` 将每条候选标记反应单独列出，用作后续界面表格的数据源。用户可编辑的核心字段是 `Selected`、`ForwardProbability`、`ReverseProbability`、`RevA`、`RevN` 和 `RevE`。其余倍率、分组和附属反应标志由程序生成。

### REV 人工输入与写入位置

当 `ExplicitRevRequirement=REV_REQUIRED` 时，界面必须在对应候选反应行显示醒目提示，并提供三个独立的人工输入框：`RevA`（逆反应指前因子）、`RevN`（逆反应温度指数）和 `RevE`（逆反应活化能）。程序不自动猜测、拟合或用正向参数代替这三个值；三项任一为空时，该反应保持 `NeedsRevParameters` 状态并禁止写出最终机理。

`REV` 的固定写入位置是对应标记反应主 Arrhenius 行的下一行，格式为：

```text
O+O*+HE=OO*+HE       3.78E+13  0.0  -1788.0
    REV / 6.563E+14 -0.055 116888.8 /
```

如反应还包含 `LOW`、`TROE`、三体增强系数或 `DUPLICATE`，这些附属行写在 `REV` 行之后。审核计划中的只读字段 `RevWritePosition=ImmediatelyAfterReactionRateLine` 用于固定该位置，`RevInputPrompt` 用于向用户说明必须人工输入逆反应 A、n、E 三参数。

同一个 `ForwardBranchGroup` 内所有已选择行的 `ForwardProbability` 必须合计为 1；同一个 `ReverseBranchGroup` 内所有已选择行的 `ReverseProbability` 必须合计为 1。程序按 `ForwardA = A_out * gf * ForwardProbability` 计算正向 A。

计划状态含义：

| 状态 | 含义 |
|---|---|
| `Ready` | 当前行可以写入最终机理 |
| `NeedsSelection` | 需要选择允许的原子映射或产物分支 |
| `NeedsProbabilities` | 需要填写正向或逆向概率 |
| `NeedsRevParameters` | 正逆倍率不一致，需要填写 `REV A/n/E` |
| `BlockedMissingSpeciesRules` | 涉及尚未定义标记形式的物种，不能继续写出 |

LOW、TROE、三体增强系数和 DUPLICATE 分别通过 `CopyLOW`、`CopyTROE`、`CopyColliderEfficiencies` 和 `Duplicate` 字段传递给后续机理写出器。

## Rewrite plan 回读与校验

使用以下命令读取用户编辑后的计划，并输出重新计算过的标准化计划：

```powershell
dotnet run --project src/TrackMechReviseTool.Cli --no-build -- validate-plan rewrite_plan_O.csv rewrite_plan_O_normalized.csv
```

校验器不信任 CSV 中已有的派生数值，而是按 `SourceA * gf * ForwardProbability` 重新计算 `ForwardRateMultiplier` 和 `ForwardA`。当正向与逆向总倍率不同时，会重新标记 `REV_REQUIRED`。

以下情况会阻止写出最终机理：

- 任一完整的 `ForwardBranchGroup` 没有选择允许分支。
- 已选择分支缺少正向或逆向概率。
- 同一正向或逆向分支组的概率和不等于 1。
- 概率超出 0 到 1，或参数不是有限数值。
- 正逆倍率不同但 `RevA`、`RevN`、`RevE` 任一为空。
- 反应涉及尚未定义标记形式的物种。
- 同一原反应内出现重复的候选编号。

验证成功时命令退出码为 0；存在规则错误时退出码为 1；CSV 格式或文件访问错误时退出码为 2。只有验证成功的标准化计划才能交给后续 `.inp` 反应段写出器。

验证通过后，可使用以下命令生成 Chemkin 标记反应段预览：

```powershell
dotnet run --project src/TrackMechReviseTool.Cli --no-build -- write-reactions MODIFY-18_gas.out rewrite_plan_O_normalized.csv marked_reactions_O.inp
```

## 从 .out 写出完整 Chemkin 机理

完整机理写出继续以 `.out` 为结构和数值来源，不从参考 `.inp` 恢复更多有效数字。程序按 `.out` 顺序写出全部原元素、原物种和原反应，并在每条对应原反应之后插入审核计划中已选择且状态为 `Ready` 的标记反应。标记反应中出现、但原物种表中不存在的物种会自动追加到 `SPECIES` 段。

完整输出包含 `ELEMENTS`、`SPECIES`、`REACTIONS` 和结尾 `END`。原反应的 `LOW`、`TROE`、三体增强系数及 `DUPLICATE` 从 `.out` 复制；标记反应按正向倍率同步缩放主反应和 `LOW` 的 A，并复制其余附属参数。需要显式逆向速率的标记反应仍将人工输入的 `REV / A n E /` 固定写在主反应行之后。

```powershell
dotnet run --project src/TrackMechReviseTool.Cli --no-build -- write-mechanism MODIFY-18_gas.out rewrite_plan_O_normalized.csv generated_mechanism_O.inp
```

写出前会重新校验计划，并核对计划中的原反应编号、方程式及原始 A、n、E 是否与当前 `.out` 匹配。任何计划错误、缺少 `REV` 参数或来源不匹配都会阻止生成完整机理。当前阶段生成的是以 `.out` 重建的完整动力学 `.inp`；标记物种的热力学 `.dat` 和输运数据扩展将在后续阶段单独生成并交叉校验。

## 标记物种热力学文件

热力学写出器读取标准四行 NASA 多项式条目，并保留输入 `.dat` 中原有内容。程序从已确认的标记反应汇总新增物种，再按元素示踪规则查找母体物种；新增标记物种继承母体物种全部 NASA 系数、温区和元素组成，仅替换首行前 18 列的物种名。例如 `O*` 继承 `O`，`OO*` 和 `O*2` 均继承 `O2`。标记符号 `*` 属于物种名，不新增化学元素，因此元素组成仍使用原元素 O 及原原子数。

```powershell
dotnet run --project src/TrackMechReviseTool.Cli --no-build -- write-thermo Zou_2023-mmc3.dat MODIFY-18_gas.out rewrite_plan_O_normalized.csv O generated_thermo_O.dat
```

若输出物种已在源热力学文件中定义，程序不会重复添加；若源 `.dat` 未覆盖 `.out` 中的任一原物种、新增物种缺少示踪映射、母体 NASA 条目缺失、存在重复活动条目，或者计划校验失败，则停止输出。该策略确保热力学系数与母体完全一致，同时避免把注释条目误认为活动条目。
