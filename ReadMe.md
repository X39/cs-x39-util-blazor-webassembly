Library offering a set of useful extensions and components for Blazor WebAssembly.

Components:
- **DashBoard** component with support for resizing and moving items.
    - Main component: `X39.Util.Blazor.WebAssembly.Components.DashBoard.DashBoard`, represents a container that organizes a single dashboard.
    - `X39.Util.Blazor.WebAssembly.Components.DashBoard.DashBoardItem` A single item on the dashboard that can be moved and resized if desired. The content of the item is provided by the `ChildContent` parameter. The `DashBoardItem` component also provides a `ResizeGrabber` and a `MoveGrabber` parameter that can be used to provide custom resize and move grabbers. The `Header` parameter can be used to provide a custom header for the item. If the default `MoveGrabber` and `Header` arrangement in HTML is not as wanted, `FullHeader` may be used to fully customize the header, including the `MoveGrabber`.
    - `X39.Util.Blazor.WebAssembly.Components.DashBoard.DashBoardDefaultResizeGrabber` A default resize grabber that can be used to resize the dashboard item. You can provide your own to the `DashBoardItem` component if desired.
    - `X39.Util.Blazor.WebAssembly.Components.DashBoard.DashBoardDefaultMoveGrabber` A default move grabber that can be used to move the dashboard item. You can provide your own to the `DashBoardItem` component if desired.

Services (use `builder.Services.AddAttributedServicesFromAssemblyOf<X39.Util.Blazor.WebAssembly.Assembly>(builder.Configuration)` to add all of them):
- `LocalStorage` offers a simple way to store and retrieve data from the browser's local storage.
- `ComponentUtil` JavaScript interop service
    - `GetElementBoundsAsync(ElementReference elementReference)` returns the values of the javascript `getBoundingClientRect()` function.
    - `RegisterOnResize(ElementReference elementReference, Func<Rectangle<double>, ValueTask> callback)` allows to register a callback that is called when the element is resized, using `ResizeObserver`.
    - `RegisterWindowEvent(string eventName, Func<JsonElement, ValueTask> callback)` allows to register a callback that is reacting to a window event, using `window.addEventListener()`.