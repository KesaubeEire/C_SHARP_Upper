// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Controls;
using WpfScada.ControlsLookup;
using WpfScada.ViewModels.Pages.BasicInput;

namespace WpfScada.Views.Pages.BasicInput;

[GalleryPage("Simple button.", SymbolRegular.ControlButton24)]
public partial class ButtonPage : INavigableView<ButtonViewModel>
{
    public ButtonViewModel ViewModel { get; }

    public ButtonPage(ButtonViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
