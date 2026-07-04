// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Controls;
using WpfScada.ControlsLookup;
using WpfScada.ViewModels.Pages.StatusAndInfo;

namespace WpfScada.Views.Pages.StatusAndInfo;

[GalleryPage("Information in popup window.", SymbolRegular.Comment24)]
public partial class ToolTipPage : INavigableView<ToolTipViewModel>
{
    public ToolTipViewModel ViewModel { get; }

    public ToolTipPage(ToolTipViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
