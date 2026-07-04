using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WpfScada.Models.Plc;
using WpfScada.Services.Plc;

namespace WpfScada.ViewModels.Pages.Plc;

public partial class RecipeViewModel : ViewModel
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

    // ===================== Current recipe metadata =====================

    [ObservableProperty]
    private string _currentRecipeName = "";

    [ObservableProperty]
    private string _currentRecipeDescription = "";

    [ObservableProperty]
    private string _currentRecipeCategory = "";

    [ObservableProperty]
    private string _currentRecipeTags = "";

    [ObservableProperty]
    private string _currentProductCode = "";

    [ObservableProperty]
    private string _currentAuthor = "";

    [ObservableProperty]
    private RecipeStatus _currentStatus = RecipeStatus.Draft;

    public ObservableCollection<RecipeStatus> StatusOptions { get; } =
        [RecipeStatus.Draft, RecipeStatus.Active, RecipeStatus.Archived];

    [ObservableProperty]
    private int _currentVersion;

    [ObservableProperty]
    private bool _hasRecipeSelected;

    [ObservableProperty]
    private int _defaultDbNumber = 1;

    // ===================== Parameter groups (tab bar) =====================

    [ObservableProperty]
    private ObservableCollection<RecipeGroup> _recipeGroups = [];

    [ObservableProperty]
    private RecipeGroup? _selectedGroup;

    [ObservableProperty]
    private ObservableCollection<RecipeParameter> _currentGroupParameters = [];

    [ObservableProperty]
    private bool _hasGroups;

    [ObservableProperty]
    private bool _hasGroupSelected;

    [ObservableProperty]
    private string _parameterSearchText = "";

    // Keep for backward compat
    [ObservableProperty]
    private ObservableCollection<string> _parameterGroups = [];

    [ObservableProperty]
    private string _selectedParameterGroup = "全部";

    // ===================== Selection =====================

    [ObservableProperty]
    private RecipeParameter? _selectedParameter;

    // ===================== Version history =====================

    [ObservableProperty]
    private ObservableCollection<RecipeVersionSnapshot> _versionHistoryItems = [];

    [ObservableProperty]
    private bool _isVersionHistoryVisible;

    [ObservableProperty]
    private RecipeVersionSnapshot? _selectedVersion;

    // ===================== PLC status =====================

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private string _plcStatusText = "未连接";

    [ObservableProperty]
    private bool _isPlcConnected;

    public RecipeViewModel(RecipeService recipeService, S7Service s7)
    {
        _recipeService = recipeService;
        _s7 = s7;
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        RefreshRecipeList();
        RefreshPlcStatus();
    }

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
                r.ProductCode.ToLowerInvariant().Contains(search) ||
                r.Tags.Any(t => t.ToLowerInvariant().Contains(search)));
        }

        if (SelectedCategoryFilter != "全部")
            filtered = filtered.Where(r => r.Category == SelectedCategoryFilter);

        FilteredRecipes = new ObservableCollection<RecipeMeta>(filtered);
    }

    // ===================== Group selection =====================

    partial void OnSelectedGroupChanged(RecipeGroup? value)
    {
        HasGroupSelected = value is not null;

        if (value is not null)
            ApplyGroupParameterFilter();
        else
            CurrentGroupParameters = [];
    }

    partial void OnParameterSearchTextChanged(string value) => ApplyGroupParameterFilter();

    private void ApplyGroupParameterFilter()
    {
        if (SelectedGroup is null)
        {
            CurrentGroupParameters = [];
            return;
        }

        var filtered = SelectedGroup.Parameters.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(ParameterSearchText))
        {
            var search = ParameterSearchText.ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.Name.ToLowerInvariant().Contains(search) ||
                p.Group.ToLowerInvariant().Contains(search));
        }

        CurrentGroupParameters = new ObservableCollection<RecipeParameter>(filtered);
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
        CurrentProductCode = "";
        CurrentAuthor = "";
        CurrentStatus = RecipeStatus.Draft;
        CurrentVersion = 1;
        DefaultDbNumber = 1;

        var defaultGroup = new RecipeGroup { Name = "参数组1" };
        RecipeGroups = [defaultGroup];
        HasGroups = true;
        SelectedGroup = defaultGroup;
        HasRecipeSelected = true;
        StatusText = "新建配方";
    }

    [RelayCommand]
    private void SaveRecipe()
    {
        if (_currentRecipe == null)
            _currentRecipe = new RecipeRecord();

        _currentRecipe.Name = CurrentRecipeName;
        _currentRecipe.Description = CurrentRecipeDescription;
        _currentRecipe.Category = CurrentRecipeCategory;
        _currentRecipe.ProductCode = CurrentProductCode;
        _currentRecipe.Author = CurrentAuthor;
        _currentRecipe.Status = CurrentStatus;
        _currentRecipe.Tags = [.. CurrentRecipeTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        _currentRecipe.DefaultDbNumber = DefaultDbNumber;

        _currentRecipe.Groups = RecipeGroups.Select(g => new RecipeGroup
        {
            Name = g.Name,
            Description = g.Description,
            Parameters = new ObservableCollection<RecipeParameter>([.. g.Parameters]),
        }).ToList();

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
        CurrentProductCode = recipe.ProductCode;
        CurrentAuthor = recipe.Author;
        CurrentStatus = recipe.Status;
        CurrentVersion = recipe.Version;
        DefaultDbNumber = recipe.DefaultDbNumber;
        HasRecipeSelected = true;
        StatusText = $"已加载「{recipe.Name}」";

        if (recipe.Groups.Count > 0)
        {
            RecipeGroups = new ObservableCollection<RecipeGroup>(recipe.Groups);
            HasGroups = true;
            SelectedGroup = RecipeGroups[0];
        }
        else
        {
            var defaultGroup = new RecipeGroup { Name = "参数组1" };
            RecipeGroups = [defaultGroup];
            HasGroups = true;
            SelectedGroup = defaultGroup;
        }

        ParameterSearchText = "";
    }

    [RelayCommand]
    private void DeleteRecipe(RecipeMeta? meta)
    {
        if (meta == null) return;

        _recipeService.DeleteRecipe(meta.Id);

        if (_currentRecipe?.Id == meta.Id)
            ClearCurrentRecipe();

        StatusText = $"配方「{meta.Name}」已删除";
        RefreshRecipeList();
    }

    private void ClearCurrentRecipe()
    {
        _currentRecipe = null;
        CurrentRecipeName = "";
        CurrentRecipeDescription = "";
        CurrentRecipeCategory = "";
        CurrentRecipeTags = "";
        CurrentProductCode = "";
        CurrentAuthor = "";
        CurrentStatus = RecipeStatus.Draft;
        CurrentVersion = 0;
        RecipeGroups = [];
        CurrentGroupParameters = [];
        HasGroups = false;
        HasRecipeSelected = false;
        SelectedGroup = null;
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

    // ===================== Group management =====================

    [RelayCommand]
    private void AddGroup()
    {
        var newGroup = new RecipeGroup
        {
            Name = $"组{RecipeGroups.Count + 1}",
        };
        RecipeGroups.Add(newGroup);
        HasGroups = true;
        SelectedGroup = newGroup;
        StatusText = $"新增参数组「{newGroup.Name}」";
    }

    [RelayCommand]
    private void RemoveGroup()
    {
        if (SelectedGroup is null) return;

        var groupName = SelectedGroup.Name;
        int index = RecipeGroups.IndexOf(SelectedGroup);
        RecipeGroups.Remove(SelectedGroup);
        HasGroups = RecipeGroups.Count > 0;

        if (RecipeGroups.Count > 0)
        {
            int newIndex = Math.Min(index, RecipeGroups.Count - 1);
            SelectedGroup = RecipeGroups[newIndex];
        }
        else
        {
            SelectedGroup = null;
            CurrentGroupParameters = [];
        }

        StatusText = $"已移除参数组「{groupName}」";
    }

    // ===================== Parameter management =====================

    [RelayCommand]
    private void AddParameter()
    {
        var currentGroup = SelectedGroup;
        if (currentGroup is null)
        {
            if (RecipeGroups.Count == 0)
            {
                currentGroup = new RecipeGroup { Name = "参数组1" };
                RecipeGroups.Add(currentGroup);
                HasGroups = true;
            }
            else
            {
                currentGroup = RecipeGroups[0];
            }
            SelectedGroup = currentGroup;
        }

        var param = new RecipeParameter
        {
            Name = $"参数{currentGroup.Parameters.Count + 1}",
            Address = (ushort)(currentGroup.Parameters.Count * 2),
            Value = 0,
            DataType = PlcDataType.Real,
        };
        currentGroup.Parameters.Add(param);
        ApplyGroupParameterFilter();
        StatusText = $"新增参数 (共 {currentGroup.Parameters.Count} 个)";
    }

    [RelayCommand]
    private void RemoveParameter(RecipeParameter? parameter)
    {
        if (parameter == null || SelectedGroup is null) return;
        SelectedGroup.Parameters.Remove(parameter);
        ApplyGroupParameterFilter();
        StatusText = $"已移除参数 (共 {SelectedGroup.Parameters.Count} 个)";
    }

    [RelayCommand]
    private void DuplicateParameter(RecipeParameter? parameter)
    {
        if (parameter == null || SelectedGroup is null) return;
        var copy = new RecipeParameter
        {
            Name = $"{parameter.Name} (副本)",
            Value = parameter.Value,
            Unit = parameter.Unit,
            Address = (ushort)(parameter.Address + 2),
            Scale = parameter.Scale,
            Offset = parameter.Offset,
            MinValue = parameter.MinValue,
            MaxValue = parameter.MaxValue,
            Group = parameter.Group,
            DataType = parameter.DataType,
            DbNumber = parameter.DbNumber,
        };
        SelectedGroup.Parameters.Add(copy);
        ApplyGroupParameterFilter();
        StatusText = $"已复制参数「{parameter.Name}」";
    }

    // ===================== Version History =====================

    [RelayCommand]
    private void ShowVersionHistory()
    {
        if (_currentRecipe?.Id is not { } id) return;

        var history = _recipeService.GetVersionHistory(id);
        VersionHistoryItems = new ObservableCollection<RecipeVersionSnapshot>(history);
        IsVersionHistoryVisible = !IsVersionHistoryVisible;
    }

    [RelayCommand]
    private void RestoreVersion(RecipeVersionSnapshot? snapshot)
    {
        if (snapshot is null || _currentRecipe is null) return;

        var restored = _recipeService.RestoreVersion(_currentRecipe.Id, snapshot.Version);
        if (restored != null)
        {
            StatusText = $"已恢复至 v{snapshot.Version}「{restored.Name}」";
            LoadRecipe(new RecipeMeta { Id = _currentRecipe.Id, Name = restored.Name });
            IsVersionHistoryVisible = false;
        }
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
        int total = _currentRecipe.Groups.Sum(g => g.Parameters.Count);
        StatusText = count >= 0
            ? $"已下载 {count}/{total} 个参数到 PLC"
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
            int total = _currentRecipe.Groups.Sum(g => g.Parameters.Count);
            StatusText = $"已从 PLC 上传 {count}/{total} 个参数";

            if (SelectedGroup is not null)
            {
                var idx = RecipeGroups.IndexOf(SelectedGroup);
                if (idx >= 0)
                {
                    SelectedGroup = RecipeGroups[idx];
                    ApplyGroupParameterFilter();
                }
            }
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
        File.WriteAllText(dialog.FileName, csv, Encoding.UTF8);
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

        var csv = ReadCsvWithAutoDetect(dialog.FileName);
        var imported = _recipeService.ImportFromCsv(csv);
        if (imported.Count == 0)
        {
            StatusText = "CSV 导入失败：未找到有效参数";
            return;
        }

        var targetGroup = SelectedGroup;
        if (targetGroup is null)
        {
            if (RecipeGroups.Count == 0)
            {
                targetGroup = new RecipeGroup { Name = "导入参数" };
                RecipeGroups.Add(targetGroup);
                HasGroups = true;
            }
            else
            {
                targetGroup = RecipeGroups[0];
            }
            SelectedGroup = targetGroup;
        }

        foreach (var param in imported)
            targetGroup.Parameters.Add(param);

        ApplyGroupParameterFilter();
        StatusText = $"已导入 {imported.Count} 个参数到「{targetGroup.Name}」";
    }

    // ===================== Refresh =====================

    [RelayCommand]
    private void RefreshRecipeList()
    {
        var all = _recipeService.GetAllRecipes();
        Recipes = new ObservableCollection<RecipeMeta>(all);
        ApplyRecipeFilter();

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
        ApplyGroupParameterFilter();
    }

    [RelayCommand]
    private void ReloadCurrentRecipe()
    {
        if (_currentRecipe?.Id is not { } id) return;
        LoadRecipe(new RecipeMeta { Id = id, Name = _currentRecipe.Name });
        StatusText = $"已重新加载「{_currentRecipe.Name}」";
    }

    /// <summary>
    /// Read CSV file with encoding auto-detection.
    /// UTF-8 BOM → UTF-8; otherwise → system ANSI via Win32 API (handles Excel-saved GBK files).
    /// </summary>
    private static string ReadCsvWithAutoDetect(string path)
    {
        var bytes = File.ReadAllBytes(path);

        // Check for UTF-8 BOM
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        // No BOM — decode as system ANSI (GBK on Chinese Windows) via Win32 API
        return DecodeAnsi(bytes);
    }

    /// <summary>Decode bytes using system ANSI code page via Win32 MultiByteToWideChar.</summary>
    private static string DecodeAnsi(byte[] bytes)
    {
        if (bytes.Length == 0) return "";
        int len = MultiByteToWideChar(CP_ACP, 0, bytes, bytes.Length, null, 0);
        if (len <= 0) return Encoding.UTF8.GetString(bytes); // fallback
        char[] chars = new char[len];
        _ = MultiByteToWideChar(CP_ACP, 0, bytes, bytes.Length, chars, len);
        return new string(chars);
    }

    private const uint CP_ACP = 0; // system default ANSI code page

    [DllImport("kernel32.dll")]
    private static extern int MultiByteToWideChar(uint codePage, uint dwFlags,
        byte[] lpMultiByteStr, int cbMultiByte,
        char[]? lpWideCharStr, int cchWideChar);
}
