// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using Wpf.Ui.Controls;

namespace Wpf.Ui.Gallery.Controls;

/// <summary>
/// 支持多级嵌套的 NavigationViewItem。
/// </summary>
public class KesaNavigationViewItem : NavigationViewItem
{
    public KesaNavigationViewItem() { }

    public KesaNavigationViewItem(Type targetPageType) : base(targetPageType) { }

    public KesaNavigationViewItem(string name, Type targetPageType) : base(name, targetPageType) { }

    public KesaNavigationViewItem(string name, SymbolRegular icon, Type targetPageType)
        : base(name, icon, targetPageType) { }

    public KesaNavigationViewItem(string name, SymbolRegular icon, Type targetPageType, IList menuItems)
        : base(name, icon, targetPageType, menuItems) { }
}
