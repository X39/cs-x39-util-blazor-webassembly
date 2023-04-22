using System.Drawing;
using System.Numerics;

namespace X39.Util.Blazor.WebAssembly.Data;

/// <summary>
/// A rectangle with a generic type.
/// </summary>
/// <param name="Left">The left side of the rectangle.</param>
/// <param name="Top">The top side of the rectangle.</param>
/// <param name="Width">The width of the rectangle.</param>
/// <param name="Height">The height of the rectangle.</param>
/// <typeparam name="T">The type of the rectangle numbers.</typeparam>
[PublicAPI]
public record struct Rectangle<T>(T Left, T Top, T Width, T Height)
    where T : INumber<T>
{

    /// <summary>
    /// Creates a new <see cref="Rectangle{T}"/> with left, top, width and height set to the same value.
    /// </summary>
    /// <param name="all">The value to set left, top, width and height to.</param>
    public Rectangle(T all) : this(all, all, all, all)
    {
    }
    /// <summary>
    /// The right side of the rectangle.
    /// </summary>
    public T Right
    {
        get => Left + Width;
        init => Width = value - Left;
    }

    /// <summary>
    /// The bottom side of the rectangle.
    /// </summary>
    public T Bottom
    {
        get => Top + Height;
        init => Height = value - Top;
    }

    /// <summary>
    /// The area of the rectangle, calculated as <see cref="Width"/> * <see cref="Height"/>.
    /// </summary>
    public T Area => Width * Height;
}

/// <summary>
/// Utility class for creating <see cref="Rectangle{T}"/>s.
/// </summary>
public static class Rectangle
{
    /// <inheritdoc cref="Rectangle{T}(T)"/>
    public static Rectangle<T> From<T>(T all) where T : INumber<T> => new(all);
    
    /// <inheritdoc cref="Rectangle{T}(T, T, T, T)"/>
    public static Rectangle<T> From<T>(T left, T top, T width, T height) where T : INumber<T> => new(left, top, width, height);
}