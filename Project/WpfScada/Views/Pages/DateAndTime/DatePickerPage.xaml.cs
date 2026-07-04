// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Controls;
using WpfScada.ControlsLookup;
using WpfScada.ViewModels.Pages.DateAndTime;

namespace WpfScada.Views.Pages.DateAndTime;

[GalleryPage("Control that lets pick a date.", SymbolRegular.CalendarSearch20)]
public partial class DatePickerPage : INavigableView<DatePickerViewModel>
{
    public DatePickerViewModel ViewModel { get; }

    public DatePickerPage(DatePickerViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
