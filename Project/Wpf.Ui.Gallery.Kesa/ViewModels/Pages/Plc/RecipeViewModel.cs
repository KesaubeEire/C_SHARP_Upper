using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.ViewModels.Pages.Plc;

public partial class RecipeViewModel : ObservableObject
{
    private readonly RecipeService _recipeService;
    private readonly S7Service _s7;
    private RecipeRecord? _currentRecipe;

    // ===================== Recipe list =====================

    [ObservableProperty]
    private ObservableCollection<RecipeMeta> _recipes = [];

    [ObservableProperty]
    private ObservableCollection<RecipeMeta> _filteredRecipes = [];

    [ObservableProperty]
    private RecipeMeta? _selectedRecipe;

    [ObservableProperty]
    private string _recipeSearchText = "";

    [ObservableProperty]
    private string _selectedCategoryFilter = "全部";

    [ObservableProperty]
    private ObservableCollection<string> _categoryOptions = ["全部"];

    // ===================== Current recipe =====================

    [ObservableProperty]
    private ObservableCollection<RecipeParameter> _currentParameters = [];

    [ObservableProperty]
    private ObservableCollection<RecipeParameter> _filteredParameters = [];

    [ObservableProperty]
    private string _currentRecipeName = "";

    [ObservableProperty]
    private string _currentRecipeDescription = "";

    [ObservableProperty]
    private string _currentRecipeCategory = "";

    [ObservableProperty]
    private string _currentRecipeTags = "";

    [ObservableProperty]
    private int _currentVersion;

    [ObservableProperty]
    private bool _hasRecipeSelected;

    // ===================== Parameter groups =====================

    [ObservableProperty]
    private ObservableCollection<string> _parameterGroups = [];

    [ObservableProperty]
    private string _selectedParameterGroup = "全部";

    [ObservableProperty]
    private string _parameterSearchText = "";

    // ===================== PLC status =====================

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private string _plcStatusText = "未连接";

    [ObservableProperty]
    private bool _isPlcConnected;

    [ObservableProperty]
    private int _defaultDbNumber = 1;

    // ===================== Selection =====================

    [ObservableProperty]
    private RecipeParameter? _selectedParameter;

    public RecipeViewModel(RecipeService recipeService, S7Service s7)
    {
        _recipeService = recipeService;
        _s7 = s7;
        RefreshRecipeList();
        RefreshPlcStatus();
    }

    // ===================== PLC status refresh =====================

    public void RefreshPlcStatus()
    {
        IsPlcConnected = _s7.IsConnected;
        PlcStatusText = _s7.IsConnected ? "已连接" : "未连接";
    }

    // ===================== Recipe selection =====================

    partial void OnSelectedRecipeChanged(RecipeMeta? value)
    {
        if (value is not null)
            LoadRecipe(value);
    }

    partial void OnRecipeSearchTextChanged(string value) => ApplyRecipeFilter();
    partial void OnSelectedCategoryFilterChanged(string value) => ApplyRecipeFilter();

    private void ApplyRecipeFilter()
    {
        var filtered = Recipes.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(RecipeSearchText))
        {
            var search = RecipeSearchText.ToLowerInvariant();
            filtered = filtered.Where(r =>
                r.Name.ToLowerInvariant().Contains(search) ||
                r.Description.ToLowerInvariant().Contains(search) ||
                r.Tags.Any(t => t.ToLowerInvariant().Contains(search)));
        }

        if (SelectedCategoryFilter != "全部")
            filtered = filtered.Where(r => r.Category == SelectedCategoryFilter);

        FilteredRecipes = new ObservableCollection<RecipeMeta>(filtered);
    }

    partial void OnParameterSearchTextChanged(string value) => ApplyParameterFilter();
    partial void OnSelectedParameterGroupChanged(string value) => ApplyParameterFilter();

    private void ApplyParameterFilter()
    {
        var filtered = CurrentParameters.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(ParameterSearchText))
        {
            var search = ParameterSearchText.ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.Name.ToLowerInvariant().Contains(search) ||
                p.Group.ToLowerInvariant().Contains(search));
        }

        if (SelectedParameterGroup != "全部")
            filtered = filtered.Where(p => p.Group == SelectedParameterGroup);

        FilteredParameters = new ObservableCollection<RecipeParameter>(filtered);
    }

    // ===================== Recipe CRUD =====================

    [RelayCommand]
    private void NewRecipe()
    {
        _currentRecipe = new RecipeRecord
        {
            Name = $"新配方 {DateTime.Now:MMdd-HHmmss}",
        };
        CurrentRecipeName = _currentRecipe.Name;
        CurrentRecipeDescription = "";
        CurrentRecipeCategory = "";
        CurrentRecipeTags = "";
        CurrentVersion = 1;
        CurrentParameters = [];
        FilteredParameters = [];
        ParameterGroups = ["全部"];
        SelectedParameterGroup = "全部";
        HasRecipeSelected = true;
        StatusText = "新建配方";
    }

    [RelayCommand]
    private void SaveRecipe()
    {
        if (_currentRecipe == null)
        {
            _currentRecipe = new RecipeRecord();
        }

        _currentRecipe.Name = CurrentRecipeName;
        _currentRecipe.Description = CurrentRecipeDescription;
        _currentRecipe.Category = CurrentRecipeCategory;
        _currentRecipe.Tags = [.. CurrentRecipeTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        _currentRecipe.DefaultDbNumber = DefaultDbNumber;
        _currentRecipe.Parameters = [.. CurrentParameters];

        _recipeService.SaveRecipe(_currentRecipe);

        CurrentVersion = _currentRecipe.Version;
        StatusText = $"配方「{_currentRecipe.Name}」已保存 (v{_currentRecipe.Version})";
        RefreshRecipeList();
    }

    [RelayCommand]
    private void LoadRecipe(RecipeMeta? meta)
    {
        if (meta == null) return;

        var recipe = _recipeService.LoadRecipe(meta.Id);
        if (recipe == null)
        {
            StatusText = "加载失败";
            return;
        }

        _currentRecipe = recipe;
        CurrentRecipeName = recipe.Name;
        CurrentRecipeDescription = recipe.Description;
        CurrentRecipeCategory = recipe.Category;
        CurrentRecipeTags = string.Join(", ", recipe.Tags);
        CurrentVersion = recipe.Version;
        DefaultDbNumber = recipe.DefaultDbNumber;
        CurrentParameters = new ObservableCollection<RecipeParameter>(recipe.Parameters);
        HasRecipeSelected = true;
        StatusText = $"已加载「{recipe.Name}」";

        // Build group list
        RefreshParameterGroups();
        SelectedParameterGroup = "全部";
        ParameterSearchText = "";
    }

    [RelayCommand]
    private void DeleteRecipe(RecipeMeta? meta)
    {
        if (meta == null) return;

        _recipeService.DeleteRecipe(meta.Id);

        if (_currentRecipe?.Id == meta.Id)
        {
            _currentRecipe = null;
            CurrentRecipeName = "";
            CurrentRecipeDescription = "";
            CurrentRecipeCategory = "";
            CurrentRecipeTags = "";
            CurrentVersion = 0;
            CurrentParameters = [];
            FilteredParameters = [];
            HasRecipeSelected = false;
            ParameterGroups = ["全部"];
        }

        StatusText = $"配方「{meta.Name}」已删除";
        RefreshRecipeList();
    }

    [RelayCommand]
    private void CopyRecipe(RecipeMeta? meta)
    {
        if (meta == null) return;

        var copy = _recipeService.CopyRecipe(meta.Id, $"{meta.Name} (副本)");
        if (copy != null)
        {
            StatusText = $"已复制为「{copy.Name}」";
            RefreshRecipeList();
        }
    }

    // ===================== Parameter management =====================

    [RelayCommand]
    private void AddParameter()
    {
        var param = new RecipeParameter
        {
            Name = $"参数{CurrentParameters.Count + 1}",
            Address = (ushort)(CurrentParameters.Count * 2),
            Value = 0,
            PlcDataType = "REAL",
        };
        CurrentParameters.Add(param);

        // Update groups
        RefreshParameterGroups();
        StatusText = $"新增参数 (共 {CurrentParameters.Count} 个)";
    }

    [RelayCommand]
    private void RemoveParameter(RecipeParameter? parameter)
    {
        if (parameter == null) return;
        CurrentParameters.Remove(parameter);
        RefreshParameterGroups();
        StatusText = $"已移除参数 (共 {CurrentParameters.Count} 个)";
    }

    [RelayCommand]
    private void DuplicateParameter(RecipeParameter? parameter)
    {
        if (parameter == null) return;
        var copy = new RecipeParameter
        {
            Name = $"{parameter.Name} (副本)",
            Value = parameter.Value,
            Unit = parameter.Unit,
            Address = (ushort)(parameter.Address + 2),
            Scale = parameter.Scale,
            Offset = parameter.Offset,
            Group = parameter.Group,
            PlcDataType = parameter.PlcDataType,
            DbNumber = parameter.DbNumber,
        };
        CurrentParameters.Add(copy);
        RefreshParameterGroups();
        StatusText = $"已复制参数「{parameter.Name}」";
    }

    private void RefreshParameterGroups()
    {
        var groups = CurrentParameters
            .Select(p => string.IsNullOrWhiteSpace(p.Group) ? "未分组" : p.Group)
            .Distinct()
            .OrderBy(g => g)
            .ToList();
        groups.Insert(0, "全部");
        ParameterGroups = new ObservableCollection<string>(groups);
        ApplyParameterFilter();
    }

    // ===================== PLC Operations =====================

    [RelayCommand]
    private async Task DownloadToPlc()
    {
        if (_currentRecipe == null)
        {
            StatusText = "请先选择配方";
            return;
        }
        if (!_s7.IsConnected)
        {
            StatusText = "PLC 未连接，无法下载";
            return;
        }

        StatusText = "正在下载到 PLC...";
        int count = _recipeService.DownloadToPlc(_currentRecipe);
        StatusText = count >= 0
            ? $"已下载 {count}/{_currentRecipe.Parameters.Count} 个参数到 PLC"
            : "下载失败";
    }

    [RelayCommand]
    private async Task UploadFromPlc()
    {
        if (_currentRecipe == null)
        {
            StatusText = "请先选择配方";
            return;
        }
        if (!_s7.IsConnected)
        {
            StatusText = "PLC 未连接，无法上传";
            return;
        }

        StatusText = "正在从 PLC 上传...";
        int count = _recipeService.UploadFromPlc(_currentRecipe);
        if (count >= 0)
        {
            StatusText = $"已从 PLC 上传 {count}/{_currentRecipe.Parameters.Count} 个参数";
            // Refresh display
            CurrentParameters = new ObservableCollection<RecipeParameter>(_currentRecipe.Parameters);
            ApplyParameterFilter();
        }
        else
        {
            StatusText = "上传失败";
        }
    }

    // ===================== CSV Import / Export =====================

    [RelayCommand]
    private void ExportCsv()
    {
        if (_currentRecipe == null) return;

        var dialog = new SaveFileDialog
        {
            Filter = "CSV 文件|*.csv|All files|*.*",
            FileName = $"{_currentRecipe.Name}.csv",
            Title = "导出配方参数为 CSV",
        };
        if (dialog.ShowDialog() != true) return;

        var csv = _recipeService.ExportToCsv(_currentRecipe);
        File.WriteAllText(dialog.FileName, csv);
        StatusText = $"已导出到 {dialog.FileName}";
    }

    [RelayCommand]
    private void ImportCsv()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV 文件|*.csv|All files|*.*",
            Title = "从 CSV 导入配方参数",
        };
        if (dialog.ShowDialog() != true) return;

        var csv = File.ReadAllText(dialog.FileName);
        var imported = _recipeService.ImportFromCsv(csv);
        if (imported.Count == 0)
        {
            StatusText = "CSV 导入失败：未找到有效参数";
            return;
        }

        foreach (var param in imported)
            CurrentParameters.Add(param);

        RefreshParameterGroups();
        StatusText = $"已导入 {imported.Count} 个参数";
    }

    // ===================== Refresh =====================

    [RelayCommand]
    private void RefreshRecipeList()
    {
        var all = _recipeService.GetAllRecipes();
        Recipes = new ObservableCollection<RecipeMeta>(all);
        ApplyRecipeFilter();

        // Refresh categories
        var cats = all
            .Select(r => r.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();
        CategoryOptions = new ObservableCollection<string>(["全部", .. cats]);
    }

    [RelayCommand]
    private void ApplyParameterGroup()
    {
        ApplyParameterFilter();
    }
}
