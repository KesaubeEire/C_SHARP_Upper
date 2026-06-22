# wpf-trend-chart

生成 LiveCharts2 趋势图/仪表盘代码片段，遵循 TrendPanel/GaugePanel 模式。

## 使用模式

### 趋势图（CartesianChart）
```xaml
<lvc:CartesianChart Series="{Binding TrendSeries}">
  <lvc:CartesianChart.XAxes>
    <lvc:Axis Labeler="value => ..." />
  </lvc:CartesianChart.XAxes>
</lvc:CartesianChart>
```

数据绑定：`ObservableCollection<ObservableValue>`，每 100ms 更新。

### 仪表盘（AngularGauge）
```xaml
<lvc:PieChart Series="{Binding GaugeSeries}">
  <lvc:XamlAngularGaugeSeries InnerRadius="..." OuterRadius="..."
      Values="{Binding ...}" />
</lvc:PieChart>
```

### 关键约定
- 主项目使用 `LiveChartsCore.SkiaSharpView.WPF` v2.0
- 所有时间序列数据用 `ObservableValue`（非 `DateTimePoint`）
- Y 轴归一化到 0-100%（使用 `TrendChannelConfig.MinRange`/`MaxRange` 映射）
- 动画速度：趋势线 100ms，条形图 400ms
- 线程安全：从 Timer 回调更新数据时使用 `Dispatcher.InvokeAsync`
