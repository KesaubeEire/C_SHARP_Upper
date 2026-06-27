// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using CommunityToolkit.Mvvm.Messaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.ViewModels.Pages;

public sealed partial class SettingsViewModel(
    INavigationService navigationService,
    AppConfigService config
) : ViewModel
{
    private bool _isInitialized = false;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private ApplicationTheme _currentApplicationTheme = ApplicationTheme.Unknown;

    [ObservableProperty]
    private NavigationViewPaneDisplayMode _currentApplicationNavigationStyle =
        NavigationViewPaneDisplayMode.Left;

    [ObservableProperty]
    private bool _showGallery;

    partial void OnShowGalleryChanged(bool value)
    {
        config.ShowGallery = value;
        config.Save();
        WeakReferenceMessenger.Default.Send(new GalleryVisibilityChangedMessage(value));
    }

    public override void OnNavigatedTo()
    {
        if (!_isInitialized)
        {
            InitializeViewModel();
        }
    }

    partial void OnCurrentApplicationThemeChanged(ApplicationTheme oldValue, ApplicationTheme newValue)
    {
        ApplicationThemeManager.Apply(newValue);
    }

    partial void OnCurrentApplicationNavigationStyleChanged(
        NavigationViewPaneDisplayMode oldValue,
        NavigationViewPaneDisplayMode newValue
    )
    {
        _ = navigationService.SetPaneDisplayMode(newValue);
    }

    private void InitializeViewModel()
    {
        CurrentApplicationTheme = ApplicationThemeManager.GetAppTheme();
        AppVersion = $"{GetAssemblyVersion()}";
        ShowGallery = config.ShowGallery;

        ApplicationThemeManager.Changed += OnThemeChanged;

        _isInitialized = true;
    }

    private void OnThemeChanged(ApplicationTheme currentApplicationTheme, Color systemAccent)
    {
        // Update the theme if it has been changed elsewhere than in the settings.
        if (CurrentApplicationTheme != currentApplicationTheme)
        {
            CurrentApplicationTheme = currentApplicationTheme;
        }
    }

    private static string GetAssemblyVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
    }
}
