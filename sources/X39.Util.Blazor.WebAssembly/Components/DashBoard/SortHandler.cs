using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Primitives;
using X39.Util.Blazor.WebAssembly.Data;
using X39.Util.Collections;
// #define DEBUG_SORT_HANDLER

namespace X39.Util.Blazor.WebAssembly.Components.DashBoard;

/// <summary>
/// Handles the sorting of <see cref="DashBoardItem"/>s.
/// </summary>
public class SortHandler
{
    private readonly IReadOnlyCollection<DashBoardItem> _dashBoardItemsMovable;
    private readonly IReadOnlyCollection<DashBoardItem> _dashBoardItemsSticky;
    private readonly double                             _columnWidth;
    private readonly double                             _rowHeight;
    private readonly int                                _gridColumns;
    private readonly int                                _gridRows;

    /// <summary>
    /// Allows you to override the grid position of a <see cref="DashBoardItem"/>.
    /// </summary>
    public Func<DashBoardItem, Rectangle<int>> GetGridPosition { get; init; } = (q) => q.GridPosition;

    /// <summary>
    /// Creates a new instance of <see cref="SortHandler"/>,
    /// allowing you to sort the positions of <see cref="DashBoardItem"/>s.
    /// </summary>
    /// <param name="dashBoardItemsMovable">The movable dash board items.</param>
    /// <param name="dashBoardItemsSticky">The non-movable dash board items.</param>
    /// <param name="columnWidth">The width of a column.</param>
    /// <param name="rowHeight">The height of a row.</param>
    /// <param name="gridColumns">The number of columns in the grid.</param>
    /// <param name="gridRows">The number of rows in the grid.</param>
    public SortHandler(
        IReadOnlyCollection<DashBoardItem> dashBoardItemsMovable,
        IReadOnlyCollection<DashBoardItem> dashBoardItemsSticky,
        double columnWidth,
        double rowHeight,
        int gridColumns,
        int gridRows)
    {
        _dashBoardItemsMovable = dashBoardItemsMovable;
        _dashBoardItemsSticky  = dashBoardItemsSticky;
        _columnWidth           = columnWidth;
        _rowHeight             = rowHeight;
        _gridColumns           = gridColumns;
        _gridRows              = gridRows;
    }

    /// <summary>
    /// Sorts the positions of the movable dash board items.
    /// </summary>
    /// <returns>The new positions of the movable dash board items.</returns>
    public (bool Success, Dictionary<DashBoardItem, (Rectangle<int> GridPosition, Rectangle<double> ActualPosition)>
        Positions) SortPositions()
    {
        var currentPosition = _dashBoardItemsSticky
            .Concat(_dashBoardItemsMovable)
            .ToDictionary((q) => q, GetGridPosition);

        // Create a grid of all possible positions and mark the positions that are already taken.
        var gridArea = new bool[_gridColumns, _gridRows];
        foreach (var dashBoardItem in _dashBoardItemsSticky)
        {
            SetArea(gridArea, currentPosition[dashBoardItem], true);
        }

        DiagArea(gridArea);

        // Adjust the positions of the movable dash board items by resizing them to the smallest possible size.
        // If the dash board item can no longer be shrunk, add it to a list of items to find a new position for.
        var unplacedDashBoardItems = AdjustPositionToRemoveOverlapAndReturnUnplacedItems(gridArea, currentPosition);

        DiagArea(gridArea);
        // Find non-overlapping, empty, rectangular areas in the grid.
        var freeAreas = FindFreeAreas(gridArea);

        // If the amount of free areas is less than the amount of unplaced dash board items, split the largest free areas in half by the larger side until there are enough free areas.
        // If all free areas reached the minimum size of 1x1, start splitting movable dash board items in half by the larger side, adding a free area for each split.
        if (!EnsureEnoughFreeAreasAvailableForUnplacedItems(
                freeAreas,
                unplacedDashBoardItems,
                currentPosition,
                gridArea))
            return (false,
                new Dictionary<DashBoardItem, (Rectangle<int> GridPosition, Rectangle<double> ActualPosition)>());

        DiagArea(gridArea);
        // Take the largest free area and place the unplaced dash board items in it.
        if (!PlaceItemsInFreeAreas(unplacedDashBoardItems, freeAreas, currentPosition, gridArea))
            return (false,
                new Dictionary<DashBoardItem, (Rectangle<int> GridPosition, Rectangle<double> ActualPosition)>());

        DiagArea(gridArea);
        if (!ResizeItemsToFitAvailableFreeSpace(
                _gridColumns,
                _gridRows,
                _dashBoardItemsMovable,
                currentPosition,
                gridArea))
            return (false,
                new Dictionary<DashBoardItem, (Rectangle<int> GridPosition, Rectangle<double> ActualPosition)>());

        DiagArea(gridArea);
        // Return the final positions.  
        return (true, currentPosition
            .ToDictionary(
                (keyValuePair) => keyValuePair.Key,
                (keyValuePair) => (GridPosition: keyValuePair.Value,
                    ActualPosition: GetDashBoardItemPosition(keyValuePair.Value, _rowHeight, _columnWidth))));
    }

    /// <summary>
    /// Increases the size of every DashBoardItem in _dashBoardItemsMovable in steps of 1,
    /// increasing towards top first, continuing to the right, then bottom, then left,
    /// until they can no longer be increased without overlapping other DashBoardItems.
    /// </summary>
    /// <param name="gridRows">The amount of available rows</param>
    /// <param name="gridColumns">The amount of available columns</param>
    /// <param name="currentPosition">Map containing the current positions of each individual <see cref="DashBoardItem"/></param>
    /// <param name="gridArea">
    /// The 2D-<see langword="bool"/> array containing information about whether
    /// a field is free (<see langword="false"/>) or not (<see langword="true"/>).
    /// </param>
    /// <param name="items">The items to resize.</param>
    /// <returns></returns>
    private static bool ResizeItemsToFitAvailableFreeSpace<T>(
        int gridColumns,
        int gridRows,
        IEnumerable<T> items,
        IDictionary<T, Rectangle<int>> currentPosition,
        bool[,] gridArea)
    {
        WriteDiagLine(nameof(ResizeItemsToFitAvailableFreeSpace));
        DiagArea(gridArea);
        var itemsList = items.ToList();
        var dashBoardItemDirectionExhausted = itemsList
            .SelectMany((q) => Enumerable.Range(0, 4).Select((w) => (dashBoardItem: q, direction: (byte) w)))
            .ToDictionary((q) => q, (_) => false);
        var sizeIncreaseDirection = -1;
        while (itemsList.Count != dashBoardItemDirectionExhausted
                   .GroupBy((q) => q.Key.dashBoardItem)
                   .Count((q) => q.Aggregate(true, (l, r) => l && r.Value)))
        {
            sizeIncreaseDirection++;
            foreach (var item in itemsList)
            {
                DiagArea(gridArea);
                if (dashBoardItemDirectionExhausted.GetValueOrDefault(
                        (dashBoardItem: item, (byte) (sizeIncreaseDirection % 4))))
                    continue;
                var gridPosition = currentPosition[item];
                DiagArea(gridPosition, gridColumns, gridRows);
                var exhausted = true;
                var newGridPosition = gridPosition;
                SetArea(gridArea, gridPosition, false);
                switch (sizeIncreaseDirection % 4)
                {
                    case 0 /* left */ when (gridPosition.Left > 0):
                    {
                        newGridPosition = gridPosition with
                        {
                            Left = gridPosition.Left - 1,
                            Width = gridPosition.Width + 1,
                        };
                        exhausted = false;
                        break;
                    }
                    case 1 /* top */ when (gridPosition.Top > 0):
                    {
                        newGridPosition = gridPosition with
                        {
                            Top = gridPosition.Top - 1,
                            Height = gridPosition.Height + 1,
                        };
                        exhausted = false;
                        break;
                    }
                    case 2 /* right */ when (gridPosition.Right < gridColumns):
                    {
                        newGridPosition = gridPosition with
                        {
                            Width = gridPosition.Width + 1,
                        };
                        exhausted = false;
                        break;
                    }
                    case 3 /* bottom */ when (gridPosition.Bottom < gridRows):
                    {
                        newGridPosition = gridPosition with
                        {
                            Height = gridPosition.Height + 1,
                        };
                        exhausted = false;
                        break;
                    }
                }

                if (IsAreaFree(gridArea, newGridPosition))
                    currentPosition[item] = gridPosition = newGridPosition;
                else
                    exhausted = true;
                SetArea(gridArea, gridPosition, true);
                dashBoardItemDirectionExhausted[(item, (byte) (sizeIncreaseDirection % 4))] = exhausted;
            }
        }

        DiagArea(gridArea);

        return true;
    }

    /// <summary>
    /// Take the largest free area and place the unplaced dash board items in it.
    /// </summary>
    /// <param name="currentPosition">Map containing the current positions of each individual <see cref="DashBoardItem"/></param>
    /// <param name="gridArea">
    /// The 2D-<see langword="bool"/> array containing information about whether
    /// a field is free (<see langword="false"/>) or not (<see langword="true"/>).
    /// </param>
    /// <param name="freeAreas"></param>
    /// <param name="items">The items to resize.</param>
    /// <returns></returns>
    private bool PlaceItemsInFreeAreas<T>(
        List<T> items,
        ICollection<Rectangle<int>> freeAreas,
        IDictionary<T, Rectangle<int>> currentPosition,
        bool[,] gridArea)
    {
        WriteDiagLine(nameof(PlaceItemsInFreeAreas));
        foreach (var dashBoardItem in items)
        {
            if (freeAreas.None())
                return false;

            var largestFreeArea = freeAreas.MaxBy((q) => q.Width * q.Height);

            WriteDiagLine($"{nameof(PlaceItemsInFreeAreas)}: Largest free area:");
            DiagArea(largestFreeArea, _gridColumns, _gridRows);
            currentPosition[dashBoardItem] = largestFreeArea;
            SetArea(gridArea, largestFreeArea, true);
            freeAreas.Remove(largestFreeArea);
        }

        return true;
    }

    private bool EnsureEnoughFreeAreasAvailableForUnplacedItems(
        ICollection<Rectangle<int>> freeAreas,
        IReadOnlyCollection<DashBoardItem> unplacedDashBoardItems,
        IDictionary<DashBoardItem, Rectangle<int>> currentPosition,
        bool[,] gridArea)
    {
        WriteDiagLine(nameof(EnsureEnoughFreeAreasAvailableForUnplacedItems));
        while (freeAreas.Count < unplacedDashBoardItems.Count)
        {
            var largestFreeArea = freeAreas.DefaultIfEmpty(Rectangle.From(-1)).MaxBy((q) => q.Width * q.Height);
            if (largestFreeArea is {Width: <= 1, Height: <= 1})
            {
                var dashBoardItem = _dashBoardItemsMovable
                    .Except(unplacedDashBoardItems)
                    .MaxBy((q) => currentPosition[q].Width * currentPosition[q].Height);
                if (dashBoardItem is null)
                {
                    return false;
                }

                var gridPosition = currentPosition[dashBoardItem];
                if (gridPosition is {Width: <= 1, Height: <= 1})
                {
                    break;
                }

                SetArea(gridArea, gridPosition, false);
                var dir = gridPosition.Width > gridPosition.Height ? 0 : 1;
                var tmpGridPosition = (dir % 2) switch
                {
                    0 => gridPosition with {Width = HalveLarge(gridPosition.Width)},
                    1 => gridPosition with {Height = HalveLarge(gridPosition.Height)},
                    _ => throw new ArithmeticException("This can never happen."),
                };
                currentPosition[dashBoardItem] = tmpGridPosition;
                SetArea(gridArea, tmpGridPosition, true);
                var newFreeArea = (dir % 2) switch
                {
                    0 => gridPosition with
                    {
                        Left = gridPosition.Left + HalveSmall(gridPosition.Width),
                        Width = HalveSmall(gridPosition.Width),
                    },
                    1 => gridPosition with
                    {
                        Top = gridPosition.Top + HalveSmall(gridPosition.Height),
                        Height = HalveSmall(gridPosition.Height),
                    },
                    _ => throw new ArithmeticException("This can never happen."),
                };
                freeAreas.Add(newFreeArea);
            }
            else
            {
                freeAreas.Remove(largestFreeArea);
                var dir = largestFreeArea.Width > largestFreeArea.Height ? 0 : 1;
                var newLargerArea = (dir % 2) switch
                {
                    0 => largestFreeArea with {Width = HalveLarge(largestFreeArea.Width)},
                    1 => largestFreeArea with {Height = HalveLarge(largestFreeArea.Height)},
                    _ => throw new ArithmeticException("This can never happen."),
                };
                var newSmallerArea = (dir % 2) switch
                {
                    0 => largestFreeArea with
                    {
                        Left = largestFreeArea.Left + HalveSmall(largestFreeArea.Width),
                        Width = HalveSmall(largestFreeArea.Width),
                    },
                    1 => largestFreeArea with
                    {
                        Top = largestFreeArea.Top + HalveSmall(largestFreeArea.Height),
                        Height = HalveSmall(largestFreeArea.Height),
                    },
                    _ => throw new ArithmeticException("This can never happen."),
                };
                freeAreas.Add(newLargerArea);
                freeAreas.Add(newSmallerArea);
            }
        }

        return true;
    }

    private static int HalveLarge(int value)
    {
        var result = value % 2 == 0 ? value / 2 : value / 2 + 1;
        return result;
    }

    private static int HalveSmall(int value)
    {
        var result = value / 2;
        return result;
    }

    private List<DashBoardItem> AdjustPositionToRemoveOverlapAndReturnUnplacedItems(
        bool[,] gridArea,
        IDictionary<DashBoardItem, Rectangle<int>> currentPosition)
    {
        WriteDiagLine(nameof(AdjustPositionToRemoveOverlapAndReturnUnplacedItems));
        var unplacedDashBoardItems = new List<DashBoardItem>();
        foreach (var dashBoardItem in _dashBoardItemsMovable)
        {
            DiagArea(gridArea);
            if (IsAreaFree(gridArea, GetGridPosition(dashBoardItem)))
            {
                WriteDiagLine($"Area {GetGridPosition(dashBoardItem)} is free for {dashBoardItem}.");
                WriteDiagLine(
                    $"{nameof(AdjustPositionToRemoveOverlapAndReturnUnplacedItems)}: Area {GetGridPosition(dashBoardItem)} is free for {dashBoardItem}.");
                SetArea(gridArea, GetGridPosition(dashBoardItem), true);
                continue;
            }

            WriteDiagLine($"Area {GetGridPosition(dashBoardItem)} is blocked for {dashBoardItem}.");

            WriteDiagLine(
                $"{nameof(AdjustPositionToRemoveOverlapAndReturnUnplacedItems)}: Area {GetGridPosition(dashBoardItem)} is not free for {dashBoardItem}.");

            byte dir = 0;
            var gridPosition = currentPosition[dashBoardItem];
            while (!IsAreaFree(gridArea, GetGridPosition(dashBoardItem)))
            {
                WriteDiagLine(
                    $"{nameof(AdjustPositionToRemoveOverlapAndReturnUnplacedItems)}: Reducing area {gridPosition} for {dashBoardItem} for direction {((dir % 4) switch {0 => "right", 1 => "down", 2 => "left", 3 => "up", _ => throw new ArithmeticException("This can never happen.")})}.");
                gridPosition = (dir % 4) switch
                {
                    0 => gridPosition with {Left = gridPosition.Left + 1},
                    1 => gridPosition with {Top = gridPosition.Top + 1},
                    2 => gridPosition with {Width = gridPosition.Width - 1},
                    3 => gridPosition with {Height = gridPosition.Height - 1},
                    _ => throw new ArithmeticException("This can never happen."),
                };
                if (gridPosition is {Width: <= 1, Height: <= 1})
                {
                    WriteDiagLine(
                        $"{nameof(AdjustPositionToRemoveOverlapAndReturnUnplacedItems)}: Area {gridPosition} is too small to proceed for {dashBoardItem}.");
                    currentPosition[dashBoardItem] = gridPosition with {Width = 1, Height = 1};
                    break;
                }

                if (gridPosition.Left < 0
                    || gridPosition.Top < 0
                    || gridPosition.Width <= 0
                    || gridPosition.Height <= 0)
                {
                    WriteDiagLine(
                        $"{nameof(AdjustPositionToRemoveOverlapAndReturnUnplacedItems)}: Area {gridPosition} is invalid for {dashBoardItem}.");
                    gridPosition = (dir % 4) switch
                    {
                        0 => gridPosition with {Left = gridPosition.Left - 1},
                        1 => gridPosition with {Top = gridPosition.Top - 1},
                        2 => gridPosition with {Width = gridPosition.Width + 1},
                        3 => gridPosition with {Height = gridPosition.Height + 1},
                        _ => throw new ArithmeticException("This can never happen."),
                    };
                    WriteDiagLine(
                        $"{nameof(AdjustPositionToRemoveOverlapAndReturnUnplacedItems)}: Resetting to {gridPosition} for {dashBoardItem}.");
                }

                dir++;
            }

            if (gridPosition is {Width: <= 1, Height: <= 1})
            {
                WriteDiagLine(
                    $"{nameof(AdjustPositionToRemoveOverlapAndReturnUnplacedItems)}: No free area found for {dashBoardItem}.");
                unplacedDashBoardItems.Add(dashBoardItem);
                currentPosition[dashBoardItem] = Rectangle.From(1);
            }
            else
            {
                WriteDiagLine(
                    $"{nameof(AdjustPositionToRemoveOverlapAndReturnUnplacedItems)}: Free area found for {dashBoardItem}: {gridPosition}.");
                currentPosition[dashBoardItem] = gridPosition;
                SetArea(gridArea, gridPosition, true);
            }
        }

        return unplacedDashBoardItems;
    }

    private List<Rectangle<int>> FindFreeAreas(bool[,] gridArea)
    {
        WriteDiagLine(nameof(FindFreeAreas));
        DiagArea(gridArea);

        var tmpGridArea = (bool[,])gridArea.Clone();
        var freeAreas = new List<Rectangle<int>>();
        Rectangle<int>? freeArea;
        while ((freeArea = FindFreeArea(tmpGridArea)) is not null)
        {
            freeAreas.Add(freeArea.Value);
            SetArea(tmpGridArea, freeArea.Value, true);
        }
        return freeAreas;
    }

    private Rectangle<int>? FindFreeArea(bool[,] gridArea)
    {
        WriteDiagLine(nameof(FindFreeAreas));
        DiagArea(gridArea);

        // Find the largest free area in the gridArea.
        // A free area is one that reports true for IsAreaFree.
        // If no free area is found, return null.
        // The smallest free area is 1x1.
        // The largest free area is the entire gridArea.
        // Finding the largest free area requires iterating over the entire gridArea, expanding the area every time until the biggest free area is found for that specific position.
        // The free areas must be kept in a list to be able to return the largest of them.
        var freeAreas = new List<Rectangle<int>>();
        for (var left = 0; left < gridArea.GetLength(0); left++)
        {
            for (var top = 0; top < gridArea.GetLength(1); top++)
            {
                // Expand width then height.
                for (var width = 1; width <= gridArea.GetLength(0) - left; width++)
                {
                    for (var height = 1; height <= gridArea.GetLength(1) - top; height++)
                    {
                        var gridPosition = Rectangle.From(left, top, width, height);
                        if (IsAreaFree(gridArea, gridPosition))
                        {
                            freeAreas.Add(gridPosition);
                        }
                    }
                }

                // Expand height then width.
                for (var height = 1; height <= gridArea.GetLength(1) - top; height++)
                {
                    for (var width = 1; width <= gridArea.GetLength(0) - left; width++)
                    {
                        var gridPosition = Rectangle.From(left, top, width, height);
                        if (IsAreaFree(gridArea, gridPosition))
                        {
                            freeAreas.Add(gridPosition);
                        }
                    }
                }
            }
        }

        return freeAreas.Any()
            ? freeAreas.MaxBy((q) => q.Area)
            : null;
    }

    private static void SetArea(bool[,] gridArea, Rectangle<int> gridPosition, bool value)
    {
        var right = Math.Min(gridArea.GetLength(0), gridPosition.Right);
        var bottom = Math.Min(gridArea.GetLength(1), gridPosition.Bottom);
        for (var x = gridPosition.Left; x < right; x++)
        for (var y = gridPosition.Top; y < bottom; y++)
            gridArea[x, y] = value;
    }

    private static bool IsAreaFree(bool[,] gridArea, Rectangle<int> gridPosition)
    {
        if (gridArea.GetLength(0) < gridPosition.Right)
            throw new ArgumentOutOfRangeException(
                nameof(gridPosition),
                $"The right position of {gridPosition} is out of bounds.");
        if (gridArea.GetLength(1) < gridPosition.Bottom)
            throw new ArgumentOutOfRangeException(
                nameof(gridPosition),
                $"The bottom position of {gridPosition} is out of bounds.");
        if (gridPosition.Left < 0)
            throw new ArgumentOutOfRangeException(
                nameof(gridPosition),
                $"The left position of {gridPosition} is out of bounds.");
        if (gridPosition.Top < 0)
            throw new ArgumentOutOfRangeException(
                nameof(gridPosition),
                $"The top position of {gridPosition} is out of bounds.");

        for (var x = gridPosition.Left; x < gridPosition.Right; x++)
        for (var y = gridPosition.Top; y < gridPosition.Bottom; y++)
            if (gridArea[x, y])
                return false;
        return true;
    }

    private static Rectangle<double> GetDashBoardItemPosition(
        Rectangle<int> gridPosition,
        double rowHeight,
        double columnWidth)
    {
        return new Rectangle<double>(
            gridPosition.Left * columnWidth,
            gridPosition.Top * rowHeight,
            gridPosition.Width * columnWidth,
            gridPosition.Height * rowHeight);
    }

    [Conditional("DEBUG_SORT_HANDLER")]
    private static void WriteDiagLine(string s)
    {
        System.Console.WriteLine(s);
    }

    [Conditional("DEBUG_SORT_HANDLER")]
    private static void DiagArea(bool[,] area)
    {
        var builder = new StringBuilder();
        builder.Append("+-");
        for (var x = 0; x < area.GetLength(0); x++)
            builder.Append(x.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("-+");
        for (var y = -1; y < area.GetLength(1) + 1; y++)
        {
            builder.Append(
                y is -1
                    ? "|"
                    : y < area.GetLength(1)
                        ? y.ToString(CultureInfo.InvariantCulture)
                        : "|");
            if (y != -1 && y < area.GetLength(0))
                for (var x = -1; x < area.GetLength(0) + 1; x++)
                    if (x == -1 || x >= area.GetLength(0))
                        builder.Append(' ');
                    else
                        builder.Append(area[x, y] ? 'X' : ' ');

            builder.AppendLine(
                y is -1
                    ? "|"
                    : y < area.GetLength(1)
                        ? y.ToString(CultureInfo.InvariantCulture)
                        : "|");
        }

        builder.Append("+-");
        for (var x = 0; x < area.GetLength(0); x++)
            builder.Append(x.ToString(CultureInfo.InvariantCulture));
        builder.Append("-+");
        System.Console.WriteLine(builder.ToString());
    }

    [Conditional("DEBUG_SORT_HANDLER")]
    private static void DiagArea(Rectangle<int> area, int columns, int rows)
    {
        var array = new bool[columns, rows];
        for (var y = area.Top; y < area.Bottom; y++)
        for (var x = area.Left; x < area.Right; x++)
            array[x, y] = true;
        DiagArea(array);
    }
}