using Microsoft.AspNetCore.Components;

namespace X39.Util.Blazor.WebAssembly.Services.Base.UiService;

public interface IUiService
{
    /// <summary>
    /// Registers a component to be notified when the UI configuration changes
    /// via <see cref="ComponentBase.StateHasChanged"/>
    /// </summary>
    /// <param name="component">The component to register.</param>
    void RegisterComponent(ComponentBase component);
}