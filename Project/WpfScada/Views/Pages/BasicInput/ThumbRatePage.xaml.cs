// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Controls;
using WpfScada.ControlsLookup;
using WpfScada.ViewModels.Pages.BasicInput;

namespace WpfScada.Views.Pages.BasicInput;

[GalleryPage("Like or dislike.", SymbolRegular.ThumbLike24)]
public partial class ThumbRatePage : INavigableView<ThumbRateViewModel>
{
    public ThumbRateViewModel ViewModel { get; }

    public ThumbRatePage(ThumbRateViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
