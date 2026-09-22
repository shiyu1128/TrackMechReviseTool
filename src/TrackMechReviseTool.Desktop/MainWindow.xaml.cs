using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TrackMechReviseTool.Core;

namespace TrackMechReviseTool.Desktop;

public partial class MainWindow : Window
{
    private ObservableCollection<PlanRowViewModel> planRows = [];
    private OutMechanism? mechanism;

    public MainWindow()
    {
        InitializeComponent();
        PlanGrid.ItemsSource = planRows;
    }

    private void BrowseOut_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 Chemkin .out 文件",
            Filter = "Chemkin output (*.out)|*.out|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            mechanism = new OutMechanismParser().Parse(dialog.FileName);
            OutPathBox.Text = dialog.FileName;
            ElementBox.ItemsSource = mechanism.MarkableElements();
            ElementBox.SelectedIndex = ElementBox.Items.Count > 0 ? 0 : -1;
            SetStatus(
                $"已载入 {mechanism.Species.Count} 个物种、{mechanism.Reactions.Count} 条反应。",
                isError: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            SetStatus($"读取 .out 失败：{exception.Message}", isError: true);
        }
    }

    private void BrowseThermo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择热力学 .dat 文件",
            Filter = "Thermodynamic data (*.dat)|*.dat|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == true)
        {
            ThermoPathBox.Text = dialog.FileName;
            SetStatus("已选择热力学文件。", isError: false);
        }
    }

    private void GeneratePlan_Click(object sender, RoutedEventArgs e)
    {
        if (mechanism is null || ElementBox.SelectedItem is not string element)
        {
            SetStatus("请先载入 .out 文件并选择标记元素。", isError: true);
            return;
        }

        try
        {
            var rows = new ReactionRewritePlanService().BuildRows(mechanism, element);
            ReplaceRows(rows);
            SetStatus($"已为元素 {element} 生成审核计划。", isError: false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            SetStatus($"生成计划失败：{exception.Message}", isError: true);
        }
    }

    private void LoadPlan_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "载入审核计划",
            Filter = "Rewrite plan (*.csv)|*.csv|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            ReplaceRows(new ReactionRewritePlanCsvReader().Read(dialog.FileName));
            SetStatus($"已载入计划：{dialog.FileName}", isError: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            SetStatus($"载入计划失败：{exception.Message}", isError: true);
        }
    }

    private void SavePlan_Click(object sender, RoutedEventArgs e)
    {
        CommitGridEdits();
        if (planRows.Count == 0)
        {
            SetStatus("当前没有可保存的审核计划。", isError: true);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "保存审核计划",
            Filter = "Rewrite plan (*.csv)|*.csv",
            FileName = "rewrite_plan.csv",
            AddExtension = true,
            DefaultExt = ".csv"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            CsvWriter.WriteReactionRewritePlan(dialog.FileName, CurrentCoreRows());
            SetStatus($"审核计划已保存：{dialog.FileName}", isError: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetStatus($"保存计划失败：{exception.Message}", isError: true);
        }
    }

    private void ValidatePlan_Click(object sender, RoutedEventArgs e)
    {
        var result = ValidateCurrentPlan();
        if (result is null)
        {
            return;
        }

        ShowValidationResult(result);
    }

    private void WriteMechanism_Click(object sender, RoutedEventArgs e)
    {
        if (mechanism is null)
        {
            SetStatus("请先载入用于写出的 .out 文件。", isError: true);
            return;
        }

        var validation = ValidateCurrentPlan();
        if (validation is null || !validation.IsValid)
        {
            ShowValidationResult(validation);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "输出完整标记机理",
            Filter = "Chemkin mechanism (*.inp)|*.inp",
            FileName = "generated_marked_mechanism.inp",
            AddExtension = true,
            DefaultExt = ".inp"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            new ChemkinMechanismWriter().Write(dialog.FileName, mechanism, validation);
            SetStatus($"完整机理已输出：{dialog.FileName}", isError: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            SetStatus($"输出机理失败：{exception.Message}", isError: true);
        }
    }

    private void WriteThermo_Click(object sender, RoutedEventArgs e)
    {
        if (mechanism is null ||
            ElementBox.SelectedItem is not string element ||
            string.IsNullOrWhiteSpace(ThermoPathBox.Text))
        {
            SetStatus("输出热力学文件前，请载入 .out、选择元素并选择源 .dat。", isError: true);
            return;
        }

        var validation = ValidateCurrentPlan();
        if (validation is null || !validation.IsValid)
        {
            ShowValidationResult(validation);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "输出标记热力学文件",
            Filter = "Thermodynamic data (*.dat)|*.dat",
            FileName = "generated_marked_thermo.dat",
            AddExtension = true,
            DefaultExt = ".dat"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var thermo = new ThermoFileParser().Parse(ThermoPathBox.Text);
            var result = new MarkedThermoWriter().Write(dialog.FileName, thermo, mechanism, validation, element);
            SetStatus(
                $"热力学文件已输出，新增 {result.AddedSpecies.Count} 个物种：{dialog.FileName}",
                isError: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
        {
            SetStatus($"输出热力学文件失败：{exception.Message}", isError: true);
        }
    }

    private ReactionRewritePlanValidationResult? ValidateCurrentPlan()
    {
        CommitGridEdits();
        if (planRows.Count == 0)
        {
            SetStatus("当前没有可校验的审核计划。", isError: true);
            return null;
        }

        var result = new ReactionRewritePlanValidator().Validate(CurrentCoreRows());
        ReplaceRows(result.NormalizedRows);
        return result;
    }

    private void ShowValidationResult(ReactionRewritePlanValidationResult? result)
    {
        if (result is null)
        {
            return;
        }

        var errors = result.Issues.Count(issue => issue.Severity == PlanValidationSeverity.Error);
        if (errors == 0)
        {
            SetStatus("计划校验通过，可以输出机理和热力学文件。", isError: false);
            return;
        }

        var firstIssue = result.Issues.First(issue => issue.Severity == PlanValidationSeverity.Error);
        SetStatus($"计划存在 {errors} 个错误。首个错误：反应 {firstIssue.SourceReactionIndex}，{firstIssue.Message}", isError: true);
    }

    private IReadOnlyList<ReactionRewritePlanRow> CurrentCoreRows()
    {
        return planRows.Select(row => row.ToCoreRow()).ToArray();
    }

    private void ReplaceRows(IEnumerable<ReactionRewritePlanRow> rows)
    {
        planRows = new ObservableCollection<PlanRowViewModel>(rows.Select(row => new PlanRowViewModel(row)));
        PlanGrid.ItemsSource = planRows;
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        var selected = planRows.Count(row => row.Selected);
        var ready = planRows.Count(row => row.PlanStatus == "Ready");
        var needsRev = planRows.Count(row => row.PlanStatus == "NeedsRevParameters");
        var invalid = planRows.Count(row => row.PlanStatus == "Invalid");
        CountText.Text = $"候选 {planRows.Count}  |  已选 {selected}  |  可写出 {ready}  |  REV 待填 {needsRev}  |  错误 {invalid}";

        RevNotice.Visibility = needsRev > 0 ? Visibility.Visible : Visibility.Collapsed;
        RevNoticeText.Text = needsRev > 0
            ? $"有 {needsRev} 条反应必须人工填写 REV A、n、E。三项完整并通过校验后，REV 行将紧跟对应反应主行写出。"
            : string.Empty;
    }

    private void CommitGridEdits()
    {
        PlanGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        PlanGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(164, 45, 45))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 52, 59));
        RefreshSummary();
    }
}
