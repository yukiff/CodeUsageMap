using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CodeUsageMap.Vsix.ViewModels;

namespace CodeUsageMap.Vsix.ToolWindows
{

public partial class UsageMapControl : UserControl
{
    private const double ZoomStep = 0.1d;
    private const double MinZoom = 0.5d;
    private const double MaxZoom = 2.0d;

    private Point? _panStartPoint;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;
    private double _zoom = 1.0d;

    public UsageMapControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyZoom();
            UpdateMiniMapViewport();
        };
    }

    private UsageMapViewModel? ViewModel => DataContext as UsageMapViewModel;

    private async void CanvasNode_OpenClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: UsageMapCanvasNodeItemViewModel node } || ViewModel is null)
        {
            return;
        }

        e.Handled = true;
        await ViewModel.OpenCanvasNodeAsync(node);
    }

    private async void CanvasNode_RerootClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: UsageMapCanvasNodeItemViewModel node } || ViewModel is null)
        {
            return;
        }

        e.Handled = true;
        await ViewModel.RerootCanvasNodeAsync(node);
    }

    private void CanvasNode_CollapseClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: UsageMapCanvasNodeItemViewModel node } || ViewModel is null)
        {
            return;
        }

        e.Handled = true;
        ViewModel.ToggleCanvasNodeCollapse(node);
    }

    private async void CanvasNode_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: UsageMapCanvasNodeItemViewModel node } || ViewModel is null)
        {
            return;
        }

        ViewModel.SelectCanvasNode(node);
        if (e.ClickCount >= 2)
        {
            e.Handled = true;
            await ViewModel.OpenCanvasNodeAsync(node);
        }
    }

    private void CanvasNode_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: UsageMapCanvasNodeItemViewModel node } || ViewModel is null)
        {
            return;
        }

        ViewModel.SelectCanvasNode(node);
    }

    private async void RootSearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is null || ViewModel.SelectedRootSearchResult is null)
        {
            return;
        }

        e.Handled = true;
        await ViewModel.ApplySelectedRootSearchResultAsync();
    }

    private void GraphCanvas_ZoomInClick(object sender, RoutedEventArgs e)
    {
        AdjustZoom(ZoomStep);
    }

    private void GraphCanvas_ZoomOutClick(object sender, RoutedEventArgs e)
    {
        AdjustZoom(-ZoomStep);
    }

    private void GraphCanvas_ResetViewClick(object sender, RoutedEventArgs e)
    {
        _zoom = 1.0d;
        ApplyZoom();
        GraphCanvasScrollViewer.ScrollToHorizontalOffset(0d);
        GraphCanvasScrollViewer.ScrollToVerticalOffset(0d);
        UpdateMiniMapViewport();
    }

    private void GraphCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        AdjustZoom(e.Delta > 0 ? ZoomStep : -ZoomStep);
        e.Handled = true;
    }

    private void GraphCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _panStartPoint = e.GetPosition(GraphCanvasScrollViewer);
        _panStartHorizontalOffset = GraphCanvasScrollViewer.HorizontalOffset;
        _panStartVerticalOffset = GraphCanvasScrollViewer.VerticalOffset;
        GraphCanvasViewport.CaptureMouse();
        GraphCanvasViewport.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void GraphCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_panStartPoint is null || e.MiddleButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPoint = e.GetPosition(GraphCanvasScrollViewer);
        var deltaX = currentPoint.X - _panStartPoint.Value.X;
        var deltaY = currentPoint.Y - _panStartPoint.Value.Y;

        GraphCanvasScrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - deltaX);
        GraphCanvasScrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - deltaY);
        e.Handled = true;
    }

    private void GraphCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            EndPan();
            e.Handled = true;
        }
    }

    private void GraphCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (e.MiddleButton != MouseButtonState.Pressed)
        {
            EndPan();
        }
    }

    private void GraphCanvasScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateMiniMapViewport();
    }

    private void GraphCanvasScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMiniMapViewport();
    }

    private void AdjustZoom(double delta)
    {
        _zoom = Math.Max(MinZoom, Math.Min(MaxZoom, _zoom + delta));
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        if (GraphCanvasScaleTransform is null)
        {
            return;
        }

        GraphCanvasScaleTransform.ScaleX = _zoom;
        GraphCanvasScaleTransform.ScaleY = _zoom;
        UpdateMiniMapViewport();
    }

    private void UpdateMiniMapViewport()
    {
        ViewModel?.UpdateMiniMapViewport(
            GraphCanvasScrollViewer.ViewportWidth,
            GraphCanvasScrollViewer.ViewportHeight,
            GraphCanvasScrollViewer.HorizontalOffset,
            GraphCanvasScrollViewer.VerticalOffset,
            _zoom);
    }

    private void EndPan()
    {
        _panStartPoint = null;
        GraphCanvasViewport.ReleaseMouseCapture();
        GraphCanvasViewport.Cursor = Cursors.Arrow;
    }
}
}
