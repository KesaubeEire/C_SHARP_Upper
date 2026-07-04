// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Controls;
using WpfScada.ControlsLookup;
using WpfScada.ViewModels.Pages.DialogsAndFlyouts;

namespace WpfScada.Views.Pages.DialogsAndFlyouts;

[GalleryPage("Snackbar notification.", SymbolRegular.Chat24)]
public partial class SnackbarPage : INavigableView<SnackbarViewModel>
{
    public SnackbarViewModel ViewModel { get; }

    public SnackbarPage(SnackbarViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
