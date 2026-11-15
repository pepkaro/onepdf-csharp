using System;
using System.Collections.Generic;

namespace OnePdf;

/// <summary>
/// Resolves colour spaces for one interpretation and remembers them, so a space named many times is parsed (and any
/// problem with it reported) once and the same instance comes back. Not thread-safe: one per interpreter.
/// </summary>
internal sealed class ColorSpaceCache(PdfDocument doc)
{
    private readonly Dictionary<string, (PdfDictionary? Resources, PdfColorSpace? Space, PdfColorSpace? Device)> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<(int Number, PdfDictionary? Resources), PdfColorSpace?> _byObject = [];
    private PdfDictionary? _deviceResources;
    private bool _deviceResourcesSet;
    private bool _hasDefaults;
    private PdfColorSpace? _gray, _rgb, _cmyk;

    /// <summary>Resolves the name operand of <c>cs</c>/<c>CS</c> (device names, <c>Pattern</c>, or a resource name).</summary>
    public PdfColorSpace? Named(string name, PdfDictionary? resources)
    {
        if (_byName.TryGetValue(name, out var hit) && ReferenceEquals(hit.Resources, resources))
        {
            return hit.Space;
        }

        PdfColorSpace? cs = ColorSpaceResolver.Resolve(doc, new PdfName(name), resources);
        _byName[name] = (resources, cs, ColorSpaceResolver.DeviceOrigin(doc, new PdfName(name), resources));
        return cs;
    }

    /// <summary>
    /// Retains a selected device space and its initial components in the graphics state. Its default is applied
    /// when the state is painted, because forms can change the resources without selecting another colour space.
    /// </summary>
    public PdfColorSpace? Selection(string name, PdfDictionary? resources, out bool device)
    {
        PdfColorSpace? resolved = Named(name, resources);
        PdfColorSpace? origin = _byName[name].Device;
        device = origin is not null;
        return resolved is null ? null : origin ?? resolved;
    }

    /// <summary>Whether the current resources contain any default device space.</summary>
    public bool HasDefaults(PdfDictionary? resources)
    {
        if (_deviceResourcesSet && ReferenceEquals(_deviceResources, resources))
        {
            return _hasDefaults;
        }

        _deviceResources = resources;
        _deviceResourcesSet = true;
        _gray = _rgb = _cmyk = null;
        PdfDictionary? spaces = resources?.GetDictionary("ColorSpace");
        _hasDefaults = spaces?.Get("DefaultGray") is not null || spaces?.Get("DefaultRGB") is not null
            || spaces?.Get("DefaultCMYK") is not null;
        return _hasDefaults;
    }

    /// <summary>Applies the current resources' default to a device colour space.</summary>
    public PdfColorSpace Device(PdfColorSpace space, PdfDictionary? resources)
    {
        if (!HasDefaults(resources))
        {
            return space;
        }

        return space.Kind switch
        {
            PdfColorSpaceKind.DeviceGray => _gray ??= Named("DeviceGray", resources) ?? space,
            PdfColorSpaceKind.DeviceCmyk => _cmyk ??= Named("DeviceCMYK", resources) ?? space,
            _ => _rgb ??= Named("DeviceRGB", resources) ?? space,
        };
    }

    /// <summary>Resolves a colour space object, such as an image's <c>/ColorSpace</c> value.</summary>
    public PdfColorSpace? Resolve(PdfObject? obj, PdfDictionary? resources)
    {
        if (obj is PdfName n)
        {
            return Named(n.Value, resources);
        }

        int key = obj?.Reference?.Number ?? -1;
        if (key >= 0 && _byObject.TryGetValue((key, resources), out PdfColorSpace? cached))
        {
            return cached;
        }

        PdfColorSpace? cs = ColorSpaceResolver.Resolve(doc, obj, resources);
        if (key >= 0)
        {
            _byObject[(key, resources)] = cs;
        }

        return cs;
    }
}

/// <summary>Turns content-stream operands into the colours a device receives.</summary>
internal static class ColorOps
{
    /// <summary>
    /// The colour for <paramref name="operands"/> in <paramref name="cs"/>, in its final space: Indexed looks up, Separation
    /// and DeviceN run the tint transform, values are clipped to the space's ranges. A Pattern space keeps itself as the
    /// colour's space and carries the tint of an uncoloured pattern converted to <see cref="PdfColorSpace.PatternBase"/>.
    /// </summary>
    public static PdfColor ToColor(PdfColorSpace cs, ReadOnlySpan<double> operands)
    {
        PdfColorSpace target = cs.Kind == PdfColorSpaceKind.Pattern ? (cs.PatternBase ?? cs) : cs.FinalSpace;
        if (cs.Kind == PdfColorSpaceKind.Pattern && cs.BaseSpace is null)
        {
            return new PdfColor(cs, 0, 0, 0, 0);
        }

        // Final spaces have at most four components (ICC 1/3/4, Lab and Cal 3 or 1).
        Span<double> f = stackalloc double[4];
        cs.ToFinalCore(operands, f[..Math.Min(target.ComponentCount, 4)]);
        return new PdfColor(cs.Kind == PdfColorSpaceKind.Pattern ? cs : target, f[0], f[1], f[2], f[3]);
    }

    /// <summary>The colour right after the space is selected (ISO 32000-2 8.6.3).</summary>
    public static PdfColor Initial(PdfColorSpace cs)
    {
        Span<double> init = stackalloc double[32];
        int n = Math.Min(cs.ComponentCount, 32);
        cs.GetInitialColor(init[..n]);
        return ToColor(cs, init[..n]);
    }
}
