namespace WpfScada;

/// <summary>
/// 当 Settings 页面中 Gallery 显示开关变化时发送的消息。
/// MainWindow 监听此消息以重建左侧菜单。
/// </summary>
public sealed record GalleryVisibilityChangedMessage(bool ShowGallery);
