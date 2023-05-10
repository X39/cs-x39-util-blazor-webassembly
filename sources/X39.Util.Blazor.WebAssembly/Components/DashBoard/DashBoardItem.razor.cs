using Microsoft.AspNetCore.Components;
using X39.Util.Blazor.WebAssembly.Data;

namespace X39.Util.Blazor.WebAssembly.Components.DashBoard;

[PublicAPI]
public partial class DashBoardItem
{
    private Rectangle<double> _rectangle;

    /// <summary>
    /// The content to be displayed inside this <see cref="DashBoardItem"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
    
    /// <summary>
    /// The header text to be displayed inside this <see cref="DashBoardItem"/>.
    /// </summary>
    /// <remarks>
    /// Only used if <see cref="Header"/> is not set by the user.
    /// </remarks>
    [Parameter]
    public string HeaderText { get; set; } = "DashBoardItem";

    /// <summary>
    /// The grabber to be displayed inside this <see cref="DashBoardItem"/>.
    /// </summary>
    /// <remarks>
    /// Due to the lack of support as of writing this component for specific typing the children,
    /// the user is responsible for ensuring that the children are of the correct type.
    /// The grabber is expected to call <see cref="OnResizeGrabberDragged"/> when the user drags the grabber.
    /// The <see cref="DashBoardItem"/> is exposed via a cascading parameter.
    /// </remarks>
    [Parameter]
    public RenderFragment ResizeGrabber { get; set; } = (builder) =>
    {
        builder.OpenComponent<DashBoardDefaultResizeGrabber>(1);
        builder.CloseComponent();
    };

    /// <summary>
    /// The grabber to be displayed inside this <see cref="DashBoardItem"/>.
    /// </summary>
    /// <remarks>
    /// Due to the lack of support as of writing this component for specific typing the children,
    /// the user is responsible for ensuring that the children are of the correct type.
    /// The grabber is expected to call <see cref="OnMoveGrabberDragged"/> when the user drags the grabber.
    /// The <see cref="DashBoardItem"/> is exposed via a cascading parameter.
    /// </remarks>
    [Parameter]
    public RenderFragment MoveGrabber { get; set; } = (builder) =>
    {
        builder.OpenComponent<DashBoardDefaultMoveGrabber>(1);
        builder.CloseComponent();
    };

    /// <summary>
    /// The header to be displayed inside this <see cref="DashBoardItem"/>.
    /// </summary>
    [Parameter]
    public RenderFragment Header { get; set; }

    /// <summary>
    /// The entire header to be displayed inside this <see cref="DashBoardItem"/>.
    /// </summary>
    /// <remarks>
    /// This should be used if the user wants to override the entire header, including the move grabber.
    /// If no move grabber is provided, the user is responsible for providing a way to move the <see cref="DashBoardItem"/>
    /// or no move will be possible.
    /// Make sure to include eg. <see cref="DashBoardDefaultMoveGrabber"/> in the header.
    /// </remarks>
    [Parameter]
    public RenderFragment FullHeader { get; set; }

    /// <summary>
    /// The position of this <see cref="DashBoardItem"/> in the <see cref="DashBoard"/>.
    /// </summary>
    /// <remarks>
    /// Note that this can be set to any value, resulting in possible overlapping of <see cref="DashBoardItem"/>'s.
    /// </remarks>
    [Parameter]
    public Rectangle<int> GridPosition { get; set; }

    /// <summary>
    /// Callback for when the <see cref="GridPosition"/> has changed.
    /// </summary>
    [Parameter]
    public EventCallback<Rectangle<int>> GridPositionChanged { get; set; }

    /// <summary>
    /// Whether this <see cref="DashBoardItem"/> can be resized by the user.
    /// </summary>
    [Parameter]
    public bool Resizable { get; set; }
    
    /// <summary>
    /// The concrete position of this <see cref="DashBoardItem"/> in the <see cref="DashBoard"/>.
    /// </summary>
    public Rectangle<double> PositionRectangle => _rectangle;

    /// <summary>
    /// All attributes you add to the component that don't match any of its parameters.
    /// They will be splatted onto the underlying HTML tag.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> UserAttributes { get; set; } = new();

    /// <summary>
    /// Creates a new <see cref="DashBoardItem"/>.
    /// </summary>
    public DashBoardItem()
    {
        Header = builder =>
        {
            builder.OpenElement(1, "span");
            builder.AddContent(2, HeaderText);
            builder.CloseElement();
        };
        FullHeader = builder =>
        {
            builder.OpenElement(1, "div");
            builder.AddAttribute(2, "style", "display: flex; border-bottom: 1px solid #ccc; padding: 5px;");

            builder.OpenComponent<CascadingValue<DashBoardItem>>(3);
            builder.AddAttribute(4, "Value", this);
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(builder2 =>
            {
                builder2.AddContent(6, MoveGrabber);
            }));
            builder.CloseComponent();
            builder.AddContent(7, Header);
            builder.CloseElement();
        };
    }
    
    /// <summary>
    /// The <see cref="DashBoard"/> this <see cref="DashBoardItem"/> is contained in.
    /// </summary>
    [CascadingParameter]
    public DashBoard DashBoard { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        await DashBoard.RegisterDashBoardItemAsync(this)
            .ConfigureAwait(false);
        await base.OnInitializedAsync()
            .ConfigureAwait(false);
    }

    public async Task<bool> OnResizeGrabberDragged()
    {
        await DashBoard.BeginResizeAsync(this)
            .ConfigureAwait(false);
        return true;
    }
    public async Task<bool> OnMoveGrabberDragged()
    {
        await DashBoard.BeginMoveAsync(this)
            .ConfigureAwait(false);
        return true;
    }

    public async Task SetPositionAsync(Rectangle<int> gridPosition, Rectangle<double> position)
    {
        _rectangle   = position;
        if (GridPosition != gridPosition)
        {
            GridPosition = gridPosition;
            await GridPositionChanged.InvokeAsync(gridPosition)
                .ConfigureAwait(false);            
        }
        await InvokeAsync(StateHasChanged)
            .ConfigureAwait(false);
    }
}