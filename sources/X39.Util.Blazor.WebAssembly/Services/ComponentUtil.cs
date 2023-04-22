using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using X39.Util.Blazor.WebAssembly.Data;
using X39.Util.Collections;
using X39.Util.DependencyInjection.Attributes;
using X39.Util.Threading;

namespace X39.Util.Blazor.WebAssembly.Services;

/// <summary>
/// Provides access to the local storage of the browser.
/// </summary>
[Singleton<ComponentUtil>]
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
[SuppressMessage("Naming", "CA1720:Bezeichner enthält Typnamen")]
public sealed class ComponentUtil : IAsyncDisposable
{
    private Lazy<IJSObjectReference> _accessor = new();
    private readonly IJSRuntime _jsRuntime;

    /// <summary>
    /// Creates a new instance of the <see cref="LocalStorage"/> class.
    /// </summary>
    /// <param name="jsRuntime">The <see cref="IJSRuntime"/> to use for accessing the local storage.</param>
    public ComponentUtil(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Ensures that the <see cref="_accessor"/> is created.
    /// </summary>
    private async Task EnsureAccessor()
    {
        if (_accessor.IsValueCreated)
            return;
        var res = await _jsRuntime.InvokeAsync<IJSObjectReference>("import",
            "./_content/X39.Util.Blazor.WebAssembly/js/exports/component-util.js");
        _accessor = new Lazy<IJSObjectReference>(res);
    }


    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_accessor.IsValueCreated)
            await _accessor.Value.DisposeAsync()
                .ConfigureAwait(false);
        await _onSizeChangeSemaphoreSlim.LockedAsync(async () =>
        {
            foreach (var elementReference in _onSizeChangeElementReferenceMap)
            {
                await _accessor.Value.InvokeVoidAsync("unregisterOnSizeChange", elementReference.Key)
                    .ConfigureAwait(false);
            }
        });
        _onSizeChangeSemaphoreSlim.Dispose();
    }

    /// <summary>
    /// Clears the local storage.
    /// </summary>
    public async Task<Rectangle<double>> GetElementBoundsAsync(ElementReference elementReference)
    {
        await EnsureAccessor()
            .ConfigureAwait(false);
        var rectangle = await _accessor.Value.InvokeAsync<Rectangle<double>>("getElementBounds", elementReference)
            .ConfigureAwait(false);
        return rectangle;
    }

    #region SizeChange

    private readonly Dictionary<ElementReference, (Guid guid, List<Func<Rectangle<double>, ValueTask>> list)>
        _onSizeChangeElementReferenceMap = new();

    private readonly Dictionary<Guid, ElementReference> _onSizeChangeGuidMap = new();

    private readonly SemaphoreSlim _onSizeChangeSemaphoreSlim = new(1, 1);

    /// <summary>
    /// Listens for changes in the size of the given element.
    /// </summary>
    /// <remarks>
    /// Make sure to dispose the returned <see cref="IAsyncDisposable"/> when you no longer need to listen for changes.
    /// </remarks>
    public async Task<IAsyncDisposable> RegisterOnChange(
        ElementReference elementReference,
        Func<Rectangle<double>, ValueTask> callback)
    {
        await EnsureAccessor()
            .ConfigureAwait(false);
        return await _onSizeChangeSemaphoreSlim.LockedAsync(async () =>
        {
            if (!_onSizeChangeElementReferenceMap.TryGetValue(elementReference, out var callbackTuple))
            {
                callbackTuple = (Guid.NewGuid(), new List<Func<Rectangle<double>, ValueTask>>());
                _onSizeChangeElementReferenceMap.Add(elementReference, callbackTuple);
                _onSizeChangeGuidMap.Add(callbackTuple.guid, elementReference);
            }

            callbackTuple.list.Add(callback);
            await _accessor.Value.InvokeVoidAsync(
                    "registerOnSizeChange",
                    elementReference,
                    callbackTuple.guid,
                    DotNetObjectReference.Create(this))
                .ConfigureAwait(false);

            return new AsyncDisposable(async () =>
            {
                await EnsureAccessor()
                    .ConfigureAwait(false);
                await _onSizeChangeSemaphoreSlim.LockedAsync(async () =>
                {
                    _onSizeChangeGuidMap.Remove(callbackTuple.guid);
                    callbackTuple.list.Remove(callback);
                    if (callbackTuple.list is {Count: > 0})
                    {
                        _onSizeChangeElementReferenceMap.Remove(elementReference);
                        await _accessor.Value.InvokeVoidAsync("unregisterOnSizeChange", elementReference)
                            .ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
            });
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// This method is called by the JS interop.
    /// For internal use only.
    /// </summary>
    [JSInvokable(nameof(OnSizeChange))]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task OnSizeChange(Guid guid, Rectangle<double> rectangle)
    {
        if (!_onSizeChangeGuidMap.TryGetValue(guid, out var elementReference))
            return;
        if (!_onSizeChangeElementReferenceMap.TryGetValue(elementReference, out var callbackTuple))
            return;
        await _onSizeChangeSemaphoreSlim.LockedAsync(async () =>
        {
            foreach (var callback in callbackTuple.list)
            {
                await Fault.IgnoreAsync(async () => await callback(rectangle).ConfigureAwait(false))
                    .ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    #endregion

    #region WindowEvents

    private readonly Dictionary<string, (Guid guid, List<Func<JsonElement, ValueTask>> list)>
        _onWindowEventReferenceMap = new();

    private readonly Dictionary<Guid, string> _onWindowEventGuidMap = new();

    private readonly SemaphoreSlim _onWindowEventSemaphoreSlim = new(1, 1);

    /// <summary>
    /// Listens for changes in the size of the given element.
    /// </summary>
    /// <remarks>
    /// Make sure to dispose the returned <see cref="IAsyncDisposable"/> when you no longer need to listen for changes.
    /// </remarks>
    public async Task<IAsyncDisposable> RegisterWindowEvent(
        [LanguageInjection(InjectedLanguage.JAVASCRIPT, Prefix = "window.addEventListener(\"", Suffix = "\", func);")]
        string windowEvent,
        Func<JsonElement, ValueTask> callback)
    {
        await EnsureAccessor()
            .ConfigureAwait(false);
        return await _onWindowEventSemaphoreSlim.LockedAsync(async () =>
        {
            if (!_onWindowEventReferenceMap.TryGetValue(windowEvent, out var callbackTuple))
            {
                callbackTuple = (Guid.NewGuid(), new List<Func<JsonElement, ValueTask>>());
                _onWindowEventReferenceMap.Add(windowEvent, callbackTuple);
                await _accessor.Value.InvokeVoidAsync(
                        "registerWindowEvent",
                        windowEvent,
                        callbackTuple.guid,
                        DotNetObjectReference.Create(this))
                    .ConfigureAwait(false);
            }

            callbackTuple.list.Add(callback);
            _onWindowEventGuidMap.Add(callbackTuple.guid, windowEvent);

            return new AsyncDisposable(async () =>
            {
                await EnsureAccessor()
                    .ConfigureAwait(false);
                await _onWindowEventSemaphoreSlim.LockedAsync(async () =>
                {
                    _onWindowEventGuidMap.Remove(callbackTuple.guid);
                    callbackTuple.list.Remove(callback);
                    if (callbackTuple.list.None())
                    {
                        _onWindowEventReferenceMap.Remove(windowEvent);
                        await _accessor.Value.InvokeVoidAsync("unregisterWindowEvent", windowEvent)
                            .ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
            });
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// This method is called by the JS interop.
    /// For internal use only.
    /// </summary>
    [JSInvokable(nameof(OnWindowEvent))]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task OnWindowEvent(Guid guid, JsonElement jsonElement)
    {
        if (!_onWindowEventGuidMap.TryGetValue(guid, out var elementReference))
            return;
        if (!_onWindowEventReferenceMap.TryGetValue(elementReference, out var callbackTuple))
            return;
        var callbackList = _onWindowEventSemaphoreSlim.Locked(() => callbackTuple.list.ToArray());
        foreach (var callback in callbackList)
        {
            await Fault.IgnoreAsync(async () => await callback(jsonElement).ConfigureAwait(false))
                .ConfigureAwait(false);
        }
    }

    #endregion
}