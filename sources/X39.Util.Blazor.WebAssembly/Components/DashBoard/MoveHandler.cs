using System.Collections.Immutable;
using System.Text.Json;
using X39.Util.Blazor.WebAssembly.Data;
using X39.Util.Blazor.WebAssembly.Services;

namespace X39.Util.Blazor.WebAssembly.Components.DashBoard;

public sealed class MoveHandler : IAsyncDisposable
{
    private readonly DashBoardItem     _dashBoardItem;
    private readonly DashBoard         _dashBoard;
    private readonly ComponentUtil     _componentUtil;
    private          IAsyncDisposable? _mouseMoveWindowEvent;
    private          IAsyncDisposable? _mouseLeaveWindowEvent;
    private          IAsyncDisposable? _mouseUpWindowEvent;
    private          Rectangle<double> _calculatedPosition;

    public MoveHandler(DashBoardItem dashBoardItem, DashBoard dashBoard, ComponentUtil componentUtil)
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
        _mouseMoveWindowEvent = null;
        if (_mouseLeaveWindowEvent is not null)
            await _mouseLeaveWindowEvent.DisposeAsync().ConfigureAwait(false);
        _mouseLeaveWindowEvent = null;
        if (_mouseUpWindowEvent is not null)
            await _mouseUpWindowEvent.DisposeAsync().ConfigureAwait(false);
        _mouseUpWindowEvent = null;
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
                Top = _calculatedPosition.Top + movementY,
                Left = _calculatedPosition.Left + movementX,
            };
            var gridPosition = new Rectangle<int>(
                Left: (int) Math.Max(Math.Round(_calculatedPosition.Left / columnWidth), 0),
                Top: (int) Math.Max(Math.Round(_calculatedPosition.Top / rowHeight), 0),
                Width: (int) Math.Max(1, Math.Round(_calculatedPosition.Width / columnWidth)),
                Height: (int) Math.Max(1, Math.Round(_calculatedPosition.Height / rowHeight)));
            System.Console.WriteLine(gridPosition);
            if (gridPosition.Right > _dashBoard.GridColumns)
                gridPosition = gridPosition with {Left = Math.Max(0, _dashBoard.GridColumns - gridPosition.Width)};
            if (gridPosition.Bottom > _dashBoard.GridRows)
                gridPosition = gridPosition with {Top = Math.Max(0, _dashBoard.GridRows - gridPosition.Height)};
            System.Console.WriteLine(gridPosition);
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