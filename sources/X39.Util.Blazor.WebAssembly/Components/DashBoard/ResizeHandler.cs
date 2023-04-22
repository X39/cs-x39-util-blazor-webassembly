using System.Collections.Immutable;
using System.Text.Json;
using X39.Util.Blazor.WebAssembly.Data;
using X39.Util.Blazor.WebAssembly.Services;

namespace X39.Util.Blazor.WebAssembly.Components.DashBoard;

public sealed class ResizeHandler : IAsyncDisposable
{
    private readonly DashBoardItem     _dashBoardItem;
    private readonly DashBoard         _dashBoard;
    private readonly ComponentUtil     _componentUtil;
    private          IAsyncDisposable? _mouseMoveWindowEvent;
    private          IAsyncDisposable? _mouseLeaveWindowEvent;
    private          IAsyncDisposable? _mouseUpWindowEvent;
    private          Rectangle<double> _calculatedPosition;

    public ResizeHandler(DashBoardItem dashBoardItem, DashBoard dashBoard, ComponentUtil componentUtil)
    {
        _dashBoardItem      = dashBoardItem;
        _dashBoard          = dashBoard;
        _componentUtil      = componentUtil;
        _calculatedPosition = _dashBoardItem.PositionRectangle;
    }

    public required Func<ValueTask<IReadOnlyCollection<DashBoardItem>>> GetItems { get; init; }
    public required Func<ValueTask<(double columnWidth, double rowHeight)>> GetGridWidthAndHeight { get; init; }
    public required Func<ValueTask> OnComplete { get; init; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_mouseMoveWindowEvent is not null)
            await _mouseMoveWindowEvent.DisposeAsync().ConfigureAwait(false);
        if (_mouseLeaveWindowEvent is not null)
            await _mouseLeaveWindowEvent.DisposeAsync().ConfigureAwait(false);
        if (_mouseUpWindowEvent is not null)
            await _mouseUpWindowEvent.DisposeAsync().ConfigureAwait(false);
    }

    public async Task InitializeAsync()
    {
        _mouseMoveWindowEvent  = await _componentUtil.RegisterWindowEvent("mousemove", OnMouseMoveAsync);
        _mouseLeaveWindowEvent = await _componentUtil.RegisterWindowEvent("mouseleave", OnMouseLeaveAsync);
        _mouseUpWindowEvent    = await _componentUtil.RegisterWindowEvent("mouseup", OnMouseUpAsync);
    }

    private async ValueTask OnMouseUpAsync(JsonElement arg)
    {
        await OnComplete()
            .ConfigureAwait(false);
    }

    private async ValueTask OnMouseLeaveAsync(JsonElement arg)
    {
        await OnComplete()
            .ConfigureAwait(false);
    }

    private async ValueTask OnMouseMoveAsync(JsonElement arg)
    {
        try
        {
            var (columnWidth, rowHeight) = await GetGridWidthAndHeight().ConfigureAwait(false);
            var movementX = arg.GetProperty("movementX").GetDouble();
            var movementY = arg.GetProperty("movementY").GetDouble();
            _calculatedPosition = _calculatedPosition with
            {
                Width = _calculatedPosition.Width + movementX,
                Height = _calculatedPosition.Height + movementY
            };
            var gridPosition = new Rectangle<int>(
                Left: (int) Math.Round(_calculatedPosition.Left / columnWidth),
                Top: (int) Math.Round(_calculatedPosition.Top / rowHeight),
                Width: (int) Math.Max(1, Math.Round(_calculatedPosition.Width / columnWidth)),
                Height: (int) Math.Max(1, Math.Round(_calculatedPosition.Height / rowHeight)));
            var dashBoardItems = await GetItems()
                .ConfigureAwait(false);
            var sortHandler = new SortHandler(
                dashBoardItems.Except(_dashBoardItem.MakeEnumerable()).ToImmutableArray(),
                _dashBoardItem.MakeArray(),
                columnWidth,
                rowHeight,
                _dashBoard.GridColumns,
                _dashBoard.GridRows)
            {
                GetGridPosition = (q) => ReferenceEquals(q, _dashBoardItem) ? gridPosition : q.GridPosition,
            };
            var (success, newPositions) = sortHandler.SortPositions();
            if (!success)
                return;
            await Task.WhenAll(
                    dashBoardItems.Select(
                            async (dashBoardItem) =>
                            {
                                await dashBoardItem
                                    .SetPositionAsync(
                                        newPositions[dashBoardItem].GridPosition,
                                        newPositions[dashBoardItem].ActualPosition)
                                    .ConfigureAwait(false);
                            })
                        .ToArray())
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            System.Console.Error.WriteLine(e);
            throw;
        }
    }
}