using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.ViewModels.Pages.Plc;

public partial class RecipeViewModel : ObservableObject
{
    private readonly RecipeService _recipeService;
    private RecipeRecord? _currentRecipe;

    [ObservableProperty]
    private ObservableCollection<RecipeMeta> _recipes = [];

    [ObservableProperty]
    private ObservableCollection<RecipeParameter> _currentParameters = [];

    [ObservableProperty]
    private string _currentRecipeName = "";

    [ObservableProperty]
    private string _currentRecipeDescription = "";

    [ObservableProperty]
    private int _currentVersion;

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private RecipeParameter? _selectedParameter;

    [ObservableProperty]
    private RecipeMeta? _selectedRecipe;

    [ObservableProperty]
    private bool _hasRecipeSelected;

    public RecipeViewModel(RecipeService recipeService)
    {
        _recipeService = recipeService;
        RefreshRecipeList();
    }

    partial void OnSelectedRecipeChanged(RecipeMeta? value)
    {
        if (value is not null)
            LoadRecipe(value);
    }

    [RelayCommand]
    private void NewRecipe()
    {
        _currentRecipe = new RecipeRecord
        {
            Name = $"新配方 {DateTime.Now:MMdd-HHmmss}",
        };
        CurrentRecipeName = _currentRecipe.Name;
        CurrentRecipeDescription = "";
        CurrentVersion = 1;
        CurrentParameters = [];
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
        CurrentVersion = recipe.Version;
        CurrentParameters = new ObservableCollection<RecipeParameter>(recipe.Parameters);
        HasRecipeSelected = true;
        StatusText = $"已加载「{recipe.Name}」";
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
            CurrentVersion = 0;
            CurrentParameters = [];
            HasRecipeSelected = false;
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

    [RelayCommand]
    private void AddParameter()
    {
        CurrentParameters.Add(new RecipeParameter
        {
            Name = $"参数{CurrentParameters.Count + 1}",
            Address = (ushort)(CurrentParameters.Count * 2),
            Value = 0,
        });
        StatusText = $"新增参数 (共 {CurrentParameters.Count} 个)";
    }

    [RelayCommand]
    private void RemoveParameter(RecipeParameter? parameter)
    {
        if (parameter == null) return;

        CurrentParameters.Remove(parameter);
        StatusText = $"已移除参数 (共 {CurrentParameters.Count} 个)";
    }

    [RelayCommand]
    private void RefreshRecipeList()
    {
        Recipes = new ObservableCollection<RecipeMeta>(_recipeService.GetAllRecipes());
    }
}
