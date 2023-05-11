using System.Collections.Immutable;
using Microsoft.AspNetCore.Components;
using X39.Util.Blazor.WebAssembly.Data;
using X39.Util.Collections;

namespace X39.Util.Blazor.WebAssembly.Components.DashBoard;

public partial class DashBoard
{
    /// <summary>
    /// The content to be displayed inside this <see cref="DashBoard"/>.
    /// </summary>
    /// <remarks>
    /// Due to the lack of support as of writing this component for specific typing the children,
    /// the user is responsible for ensuring that the children are of the correct type.
    /// The content should be <see cref="DashBoardItem"/>'s.
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// The amount of user-adjustable columns in the grid.
    /// </summary>
    [Parameter]
    public int GridColumns { get; set; } = 12;

    /// <summary>
    /// The amount of user-adjustable rows in the grid.
    /// </summary>
    [Parameter]
    public int GridRows { get; set; } = 12;

    /// <summary>
    /// All attributes you add to the component that don't match any of its parameters.
    /// They will be splatted onto the underlying HTML tag.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> UserAttributes { get; set; } = new();


    private readonly List<WeakReference<DashBoardItem>> _dashBoardItems = new();

    internal async Task RegisterDashBoardItemAsync(DashBoardItem dashBoardItem)
    {
        if (dashBoardItem == null)
            throw new ArgumentNullException(nameof(dashBoardItem));
        lock (_dashBoardItems)
        {
            _dashBoardItems.RemoveAll(weakReference => !weakReference.TryGetTarget(out _));
            _dashBoardItems.Add(new WeakReference<DashBoardItem>(dashBoardItem));
        }

        var (columnWidth, rowHeight) = await GetGridWidthAndHeightAsync();
        var position = GetDashBoardItemPosition(dashBoardItem, rowHeight, columnWidth);
        await dashBoardItem.SetPositionAsync(dashBoardItem.GridPosition, position)
            .ConfigureAwait(false);
    }

    private static Rectangle<double> GetDashBoardItemPosition(
        DashBoardItem dashBoardItem,
        double rowHeight,
        double columnWidth)
    {
        return new Rectangle<double>(
            dashBoardItem.GridPosition.Left * columnWidth,
            dashBoardItem.GridPosition.Top * rowHeight,
            dashBoardItem.GridPosition.Width * columnWidth,
            dashBoardItem.GridPosition.Height * rowHeight);
    }

    private async ValueTask<Rectangle<double>> GetBoundsAsync()
    {
        var elementBounds = await ComponentUtil.GetElementBoundsAsync(_dashBoardDiv)
            .ConfigureAwait(false);
        return elementBounds;
    }

    private async ValueTask OnResizeAsync(Rectangle<double> rectangle)
    {
        var (columnWidth, rowHeight) = await GetGridWidthAndHeightAsync();
        var dashBoardItems = GetDashBoardItems();
        foreach (var dashBoardItem in dashBoardItems)
        {
            var position = GetDashBoardItemPosition(dashBoardItem, rowHeight, columnWidth);
            await dashBoardItem.SetPositionAsync(dashBoardItem.GridPosition, position)
                .ConfigureAwait(false);
        }
    }

    private async Task<(double columnWidth, double rowHeight)> GetGridWidthAndHeightAsync()
    {
        var elementBounds = await GetBoundsAsync()
            .ConfigureAwait(false);
        if (elementBounds is not ({Width: 0} or {Height: 0}))
            return GetGridWidthAndHeight(elementBounds);
        await System.Console.Error.WriteLineAsync($"DashBoard width or height is 0 ({elementBounds}).");
        elementBounds = elementBounds with
        {
            Width = Math.Max(elementBounds.Width, 1),
            Height = Math.Max(elementBounds.Height, 1),
        };

        return GetGridWidthAndHeight(elementBounds);
    }

    private (double columnWidth, double rowHeight) GetGridWidthAndHeight(Rectangle<double> elementBounds)
    {
        var columnWidth = elementBounds.Width / GridColumns;
        var rowHeight = elementBounds.Height / GridRows;
        return (columnWidth, rowHeight);
    }

    private IReadOnlyCollection<DashBoardItem> GetDashBoardItems()
    {
        var dashBoardItems = new List<DashBoardItem>();
        lock (_dashBoardItems)
        {
            _dashBoardItems.RemoveAll(weakReference => !weakReference.TryGetTarget(out _));
            foreach (var weakReference in _dashBoardItems)
            {
                if (weakReference.TryGetTarget(out var dashBoardItem))
                {
                    dashBoardItems.Add(dashBoardItem);
                }
            }
        }

        return dashBoardItems;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _registerOnChange = await ComponentUtil.RegisterOnResize(_dashBoardDiv, OnResizeAsync)
            .ConfigureAwait(false);
        await base.OnAfterRenderAsync(firstRender)
            .ConfigureAwait(false);
    }

    private ElementReference  _dashBoardDiv;
    private IAsyncDisposable? _registerOnChange;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_registerOnChange is not null)
            await _registerOnChange.DisposeAsync()
                .ConfigureAwait(false);
        if (_moveHandler is not null)
            await _moveHandler.DisposeAsync()
                .ConfigureAwait(false);
    }

    private ResizeHandler? _resizeHandler;
    private MoveHandler?   _moveHandler;

    public async Task BeginResizeAsync(DashBoardItem dashBoardItem)
    {
        if (_resizeHandler is not null)
            await _resizeHandler.DisposeAsync()
                .ConfigureAwait(false);
        _resizeHandler = new ResizeHandler(dashBoardItem, this, ComponentUtil)
        {
            GetItems = () =>
            {
                lock (_dashBoardItems)
                {
                    var dashBoardItems = _dashBoardItems
                        .Select((q) => q.TryGetTarget(out var target) ? target : null)
                        .NotNull()
                        .ToImmutableArray();
                    return ValueTask.FromResult<IReadOnlyCollection<DashBoardItem>>(dashBoardItems);
                }
            },
            GetGridWidthAndHeight = async () => await GetGridWidthAndHeightAsync().ConfigureAwait(false),
            OnComplete = async () =>
            {
                await _resizeHandler.DisposeAsync()
                    .ConfigureAwait(false);
                _resizeHandler = null;
            },
        };
        _ = _resizeHandler.InitializeAsync().ConfigureAwait(false);
    }
    public async Task BeginMoveAsync(DashBoardItem dashBoardItem)
    {
        if (_resizeHandler is not null)
            await _resizeHandler.DisposeAsync()
                .ConfigureAwait(false);
        _moveHandler = new MoveHandler(dashBoardItem, this, ComponentUtil)
        {
            GetItems = () =>
            {
                lock (_dashBoardItems)
                {
                    var dashBoardItems = _dashBoardItems
                        .Select((q) => q.TryGetTarget(out var target) ? target : null)
                        .NotNull()
                        .ToImmutableArray();
                    return ValueTask.FromResult<IReadOnlyCollection<DashBoardItem>>(dashBoardItems);
                }
            },
            GetGridWidthAndHeight = async () => await GetGridWidthAndHeightAsync().ConfigureAwait(false),
            OnComplete = async () =>
            {
                await _moveHandler.DisposeAsync()
                    .ConfigureAwait(false);
                _moveHandler = null;
            },
        };
        _ = _moveHandler.InitializeAsync().ConfigureAwait(false);
    }
}