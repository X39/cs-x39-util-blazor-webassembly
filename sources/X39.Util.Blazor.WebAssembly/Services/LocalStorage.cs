using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.JSInterop;
using X39.Util.DependencyInjection.Attributes;

namespace X39.Util.Blazor.WebAssembly.Services;

/// <summary>
/// Provides access to the local storage of the browser.
/// </summary>
[Singleton<LocalStorage>]
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public sealed class LocalStorage : IAsyncDisposable
{
    private          Lazy<IJSObjectReference> _accessor = new();
    private readonly IJSRuntime               _jsRuntime;

    /// <summary>
    /// Creates a new instance of the <see cref="LocalStorage"/> class.
    /// </summary>
    /// <param name="jsRuntime">The <see cref="IJSRuntime"/> to use for accessing the local storage.</param>
    public LocalStorage(IJSRuntime jsRuntime)
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
        var res = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/exports/local-storage.js");
        _accessor = new Lazy<IJSObjectReference>(res);
    }


    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_accessor.IsValueCreated)
            await _accessor.Value.DisposeAsync()
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the given value for the given key in the local storage.
    /// </summary>
    /// <param name="key">The key to set the value for.</param>
    /// <param name="value">The value to set.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SetAsync<T>(string key, T value) where T : notnull
    {
        await EnsureAccessor().ConfigureAwait(false);
        var json = JsonSerializer.SerializeToElement(new JsonObjectWrapper<T>(value));
        await _accessor.Value.InvokeVoidAsync("set", key, json.ToString()).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the value for the given key from the local storage.
    /// </summary>
    /// <param name="key">The key to get the value for.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <returns>The value for the given key or null if the key does not exist.</returns>
    public async Task<T?> GetAsync<T>(string key) where T : notnull
    {
        await EnsureAccessor().ConfigureAwait(false);
        var result = await _accessor.Value.InvokeAsync<string>("get", key).ConfigureAwait(false);
        if (result.IsNullOrEmpty())
            return default;
        var objectified = JsonSerializer.Deserialize<JsonObjectWrapper<T>>(result);
        return objectified is null ? default : objectified.Value;
    }

    /// <summary>
    /// Removes the given key from the local storage.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    public async Task RemoveAsync(string key)
    {
        await EnsureAccessor().ConfigureAwait(false);
        await _accessor.Value.InvokeVoidAsync("remove", key).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears the local storage.
    /// </summary>
    public async Task ClearAsync()
    {
        await EnsureAccessor().ConfigureAwait(false);
        await _accessor.Value.InvokeVoidAsync("clear").ConfigureAwait(false);
    }

    /// <summary>
    /// Allows to wrap around every object making it easier to serialize and deserialize the value to and from json.
    /// </summary>
    /// <remarks>
    /// This is used because of some limitations of the <see cref="JsonSerializer"/>.
    /// It is sometimes not possible to serialize and deserialize a value type directly making usage of this wrapper
    /// necessary, given the code should be as simple to read as possible.
    /// </remarks>
    /// <param name="Value">The value.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    private record JsonObjectWrapper<T>(T Value);
}