using System;
using System.Collections.Generic;

namespace OnePdf;

/// <summary>Builds <see cref="PdfColorSpace"/> values from COS objects (ISO 32000-2 8.6.3 to 8.6.6).</summary>
internal static class ColorSpaceResolver
{
    private const int MaxDeviceNComponents = 32;

    public static PdfColorSpace? Resolve(PdfDocument doc, PdfObject? obj, PdfDictionary? resources, int objectNumber = -1)
    {
        return Resolve(doc, obj, resources, 0, objectNumber, allowPattern: true, useDefaults: true);
    }

    /// <summary>The original top-level device space, including resource aliases, before default substitution.</summary>
    public static PdfColorSpace? DeviceOrigin(PdfDocument doc, PdfObject? obj, PdfDictionary? resources)
    {
        for (int depth = 0; depth <= Math.Min(doc.Limits.MaxRecursionDepth, 8); depth++)
        {
            bool array = obj is PdfArray;
            if (obj is PdfArray a)
            {
                obj = a.Count > 0 ? a[0] : null;
            }

            if (obj is not PdfName name)
            {
                return null;
            }

            PdfColorSpace? device = name.Value switch
            {
                "DeviceGray" or "G" => PdfColorSpace.Gray,
                "DeviceRGB" or "RGB" => PdfColorSpace.Rgb,
                "DeviceCMYK" or "CMYK" => PdfColorSpace.Cmyk,
                _ => null,
            };
            if (device is not null || array)
            {
                return device;
            }

            obj = resources?.GetDictionary("ColorSpace")?.Get(name.Value);
        }

        return null;
    }

    private static PdfColorSpace? Resolve(PdfDocument doc, PdfObject? obj, PdfDictionary? resources, int depth, int objNum, bool allowPattern, bool useDefaults)
    {
        // Spaces nest only a few levels in real files; the recursion limit also stops self-referencing arrays.
        if (depth > Math.Min(doc.Limits.MaxRecursionDepth, 8))
        {
            doc.ReportWarning("Colour space nesting is too deep or cyclic.", objNum, -1);
            return null;
        }

        switch (obj)
        {
            case PdfName n:
                return FromName(doc, n.Value, resources, depth, objNum, allowPattern, useDefaults);
            case PdfArray a when a.Count > 0 && a[0] is PdfName fam:
                return FromArray(doc, a, fam.Value, resources, depth, objNum, allowPattern, useDefaults);
            default:
                doc.ReportWarning("A colour space is neither a name nor an array.", objNum, -1);
                return null;
        }
    }

    private static PdfColorSpace? FromName(PdfDocument doc, string name, PdfDictionary? resources, int depth, int objNum, bool allowPattern, bool useDefaults)
    {
        switch (name)
        {
            case "DeviceGray" or "G":
                return Device(doc, PdfColorSpace.Gray, "DefaultGray", resources, depth, objNum, useDefaults);
            case "CalGray":
                return PdfColorSpace.Gray;
            case "DeviceRGB" or "RGB":
                return Device(doc, PdfColorSpace.Rgb, "DefaultRGB", resources, depth, objNum, useDefaults);
            case "CalRGB":
                return PdfColorSpace.Rgb;
            case "DeviceCMYK" or "CMYK":
                return Device(doc, PdfColorSpace.Cmyk, "DefaultCMYK", resources, depth, objNum, useDefaults);
            case "Pattern":
                return allowPattern ? new PdfColorSpace(PdfColorSpaceKind.Pattern, 0) : null;
        }

        if (resources?.GetDictionary("ColorSpace") is { } dict && dict.Get(name) is { } found)
        {
            // A resource may itself be a name (an alias); the depth counter bounds loops.
            return Resolve(doc, found, resources, depth + 1, objNum, allowPattern, useDefaults);
        }

        doc.ReportWarning("Colour space /" + name + " is not defined.", objNum, -1);
        return null;
    }

    private static PdfColorSpace? FromArray(PdfDocument doc, PdfArray a, string family, PdfDictionary? resources, int depth, int objNum, bool allowPattern, bool useDefaults)
    {
        switch (family)
        {
            case "DeviceGray" or "G" or "DeviceRGB" or "RGB" or "DeviceCMYK" or "CMYK":
                return FromName(doc, family, resources, depth, objNum, allowPattern, useDefaults);
            case "CalGray":
                return Cal(doc, a, PdfColorSpaceKind.CalGray, 1, objNum);
            case "CalRGB":
                return Cal(doc, a, PdfColorSpaceKind.CalRgb, 3, objNum);
            case "Lab":
                return Cal(doc, a, PdfColorSpaceKind.Lab, 3, objNum);
            case "ICCBased":
                return Icc(doc, a, resources, depth, objNum);
            case "Indexed" or "I":
                return Indexed(doc, a, resources, depth, objNum, useDefaults);
            case "Separation":
                return Tinted(doc, a, resources, depth, objNum, device: false, useDefaults);
            case "DeviceN":
                return Tinted(doc, a, resources, depth, objNum, device: true, useDefaults);
            case "Pattern":
            {
                var p = new PdfColorSpace(PdfColorSpaceKind.Pattern, 0);
                if (a.Count > 1 && Resolve(doc, a[1], resources, depth + 1, objNum, allowPattern: false, useDefaults) is { } u)
                {
                    return new PdfColorSpace(PdfColorSpaceKind.Pattern, u.ComponentCount) { BaseSpace = u };
                }

                return p;
            }

            default:
                doc.ReportWarning("Unknown colour space family /" + family + ".", objNum, -1);
                return null;
        }
    }

    private static PdfColorSpace Device(PdfDocument doc, PdfColorSpace device, string name, PdfDictionary? resources, int depth, int objNum, bool useDefaults)
    {
        if (!useDefaults || resources?.GetDictionary("ColorSpace")?.Get(name) is not { } replacement)
        {
            return device;
        }

        // ISO 32000-1 8.6.5.6: pass device components through to a compatible default. Do not remap
        // device spaces inside the replacement again (a device default or its alternate may name itself).
        PdfColorSpace? space = Resolve(doc, replacement, resources, depth + 1, objNum, allowPattern: false, useDefaults: false);
        if (space is not null && space.ComponentCount == device.ComponentCount
            && space.Kind is not (PdfColorSpaceKind.Lab or PdfColorSpaceKind.Indexed or PdfColorSpaceKind.Pattern))
        {
            return space;
        }

        doc.ReportWarning("Colour space /" + name + " is not a compatible default; using the device space.", objNum, -1);
        return device;
    }

    private static double[]? Numbers(PdfObject? o, int count)
    {
        if (o is not PdfArray arr || arr.Count < count)
        {
            return null;
        }

        var r = new double[arr.Count];
        for (int i = 0; i < r.Length; i++)
        {
            r[i] = arr[i] switch
            {
                PdfInteger n => n.Value,
                PdfReal f => double.IsFinite(f.Value) ? f.Value : 0,
                _ => 0,
            };
        }

        return r;
    }

    private static PdfColorSpace? Cal(PdfDocument doc, PdfArray a, PdfColorSpaceKind kind, int n, int objNum)
    {
        PdfDictionary? d = a.Count > 1 ? a[1] as PdfDictionary : null;
        if (d is null)
        {
            doc.ReportWarning("A " + kind + " colour space has no parameter dictionary; using the device space.", objNum, -1);
            return n == 1 ? PdfColorSpace.Gray : PdfColorSpace.Rgb;
        }

        double[]? white = Numbers(d.Get("WhitePoint"), 3);
        if (white is null || white[1] <= 0)
        {
            doc.ReportWarning(kind + " has no valid /WhitePoint; using D50.", objNum, -1);
            white = [.. PdfColorSpace.DefaultWhitePoint];
        }

        var cs = new PdfColorSpace(kind, n)
        {
            WhitePoint = white[..3],
            BlackPoint = Numbers(d.Get("BlackPoint"), 3)?[..3] ?? [0, 0, 0],
        };
        if (kind == PdfColorSpaceKind.CalGray)
        {
            double g = d.GetDouble("Gamma", 1);
            cs.Gamma = [g > 0 ? g : 1];
        }
        else if (kind == PdfColorSpaceKind.CalRgb)
        {
            double[]? g = Numbers(d.Get("Gamma"), 3);
            cs.Gamma = g is null || g[0] <= 0 || g[1] <= 0 || g[2] <= 0 ? [1, 1, 1] : g[..3];
            cs.Matrix = Numbers(d.Get("Matrix"), 9)?[..9] ?? [1, 0, 0, 0, 1, 0, 0, 0, 1];
        }
        else
        {
            double[]? r = Numbers(d.Get("Range"), 4);
            double[] range = r is null ? [-100, 100, -100, 100] : r[..4];
            if (range[1] < range[0] || range[3] < range[2])
            {
                range = [-100, 100, -100, 100];
            }

            cs.Range = range;
        }

        return cs;
    }

    private static PdfColorSpace? Icc(PdfDocument doc, PdfArray a, PdfDictionary? resources, int depth, int objNum)
    {
        if (a.Count < 2 || a[1] is not PdfStream s)
        {
            doc.ReportWarning("An ICCBased colour space has no profile stream.", objNum, -1);
            return null;
        }

        int n = (int)Math.Clamp(s.Dictionary.GetInt("N", 0), 0, 1000);
        PdfColorSpace? alt = null;
        if (s.Dictionary.Get("Alternate") is { } altObj)
        {
            alt = Resolve(doc, altObj, resources, depth + 1, objNum, allowPattern: false, useDefaults: false);
        }

        if (n is not (1 or 3 or 4))
        {
            // N is required; recover from the alternate when it is a usable device space.
            if (alt is { IsFinal: true })
            {
                doc.ReportWarning("ICCBased /N is missing or invalid; using the /Alternate space.", objNum, -1);
                return alt;
            }

            doc.ReportWarning("ICCBased /N is missing or invalid.", objNum, -1);
            return null;
        }

        ReadOnlyMemory<byte> profile = ReadOnlyMemory<byte>.Empty;
        try
        {
            profile = s.GetDecodedBytes();
        }
        catch (PdfLimitException)
        {
            doc.ReportWarning("An ICC profile is larger than the stream limits; the device space is used.", objNum, -1);
        }

        // ICC.1 header: 128 bytes, signature 'acsp' at offset 36, colour space signature at 16.
        bool plausible = profile.Length >= 128 && profile.Span[36] == (byte)'a' && profile.Span[37] == (byte)'c' && profile.Span[38] == (byte)'s' && profile.Span[39] == (byte)'p'
            && ProfileComponents(profile.Span.Slice(16, 4)) is var pc && (pc == n || pc == 0);
        if (!plausible)
        {
            doc.ReportWarning("An ICC profile is damaged or does not match /N; the device space is used.", objNum, -1);
            PdfColorSpace device = alt is { IsFinal: true } && alt.ComponentCount == n ? alt : DeviceFor(n);
            return device;
        }

        var cs = new PdfColorSpace(PdfColorSpaceKind.IccBased, n)
        {
            IccProfile = profile,
            Alternate = alt is { IsFinal: true } && alt.ComponentCount == n ? alt : null,
        };
        double[]? range = Numbers(s.Dictionary.Get("Range"), n * 2);
        if (range is not null)
        {
            bool ok = true;
            for (int i = 0; i < n; i++)
            {
                ok &= range[i * 2] <= range[i * 2 + 1];
            }

            if (ok)
            {
                cs.Range = range[..(n * 2)];
            }
        }

        return cs;
    }

    private static PdfColorSpace DeviceFor(int n) => n switch
    {
        1 => PdfColorSpace.Gray,
        4 => PdfColorSpace.Cmyk,
        _ => PdfColorSpace.Rgb,
    };

    /// <summary>Components for the ICC data colour space signature, or 0 when it is one this reader cannot map.</summary>
    private static int ProfileComponents(ReadOnlySpan<byte> sig) => sig switch
    {
        [(byte)'G', (byte)'R', (byte)'A', (byte)'Y'] => 1,
        [(byte)'R', (byte)'G', (byte)'B', (byte)' '] => 3,
        [(byte)'L', (byte)'a', (byte)'b', (byte)' '] => 3,
        [(byte)'X', (byte)'Y', (byte)'Z', (byte)' '] => 3,
        [(byte)'C', (byte)'M', (byte)'Y', (byte)'K'] => 4,
        _ => 0,
    };

    private static PdfColorSpace? Indexed(PdfDocument doc, PdfArray a, PdfDictionary? resources, int depth, int objNum, bool useDefaults)
    {
        if (a.Count < 4)
        {
            doc.ReportWarning("An Indexed colour space needs four elements.", objNum, -1);
            return null;
        }

        PdfColorSpace? b = Resolve(doc, a[1], resources, depth + 1, objNum, allowPattern: false, useDefaults);
        if (b is null || b.Kind == PdfColorSpaceKind.Indexed)
        {
            doc.ReportWarning("An Indexed colour space has no usable base space.", objNum, -1);
            return null;
        }

        long hival = a[2] switch
        {
            PdfInteger i => i.Value,
            PdfReal r => (long)Math.Clamp(r.Value, -1, 1000),
            _ => 0,
        };
        if (hival is < 0 or > 255)
        {
            doc.ReportWarning("Indexed /hival " + hival + " is outside 0..255; clipped.", objNum, -1);
            hival = Math.Clamp(hival, 0, 255);
        }

        ReadOnlyMemory<byte> table = a[3] switch
        {
            PdfString s => s.Bytes,
            PdfStream st => SafeDecode(doc, st, objNum),
            _ => ReadOnlyMemory<byte>.Empty,
        };
        long need = (hival + 1) * b.ComponentCount;
        if (table.Length < need)
        {
            doc.ReportWarning("An Indexed lookup table holds " + table.Length + " bytes, " + need + " needed; the rest reads as 0.", objNum, -1);
        }
        else if (table.Length > need)
        {
            table = table[..(int)need];
        }

        return new PdfColorSpace(PdfColorSpaceKind.Indexed, 1) { BaseSpace = b, HiVal = (int)hival, Lookup = table };
    }

    private static ReadOnlyMemory<byte> SafeDecode(PdfDocument doc, PdfStream st, int objNum)
    {
        try
        {
            return st.GetDecodedBytes();
        }
        catch (PdfLimitException)
        {
            doc.ReportWarning("An Indexed lookup stream exceeds the stream limits; read as empty.", objNum, -1);
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    private static PdfColorSpace? Tinted(PdfDocument doc, PdfArray a, PdfDictionary? resources, int depth, int objNum, bool device, bool useDefaults)
    {
        if (a.Count < 4)
        {
            doc.ReportWarning("A Separation or DeviceN colour space needs four elements.", objNum, -1);
            return null;
        }

        var names = new List<string>();
        if (!device)
        {
            names.Add(a.GetName(1) ?? string.Empty);
        }
        else if (a[1] is PdfArray na)
        {
            for (int i = 0; i < na.Count && names.Count <= MaxDeviceNComponents; i++)
            {
                names.Add(na.GetName(i) ?? string.Empty);
            }
        }

        if (names.Count is 0 or > MaxDeviceNComponents)
        {
            doc.ReportWarning("A DeviceN colour space must have 1 to " + MaxDeviceNComponents + " colorants.", objNum, -1);
            return null;
        }

        PdfColorSpace? alt = Resolve(doc, a[2], resources, depth + 1, objNum, allowPattern: false, useDefaults);
        if (alt is null || alt.Kind is PdfColorSpaceKind.Pattern)
        {
            doc.ReportWarning("A Separation or DeviceN colour space has no usable alternate space.", objNum, -1);
            return null;
        }

        IColorTransform? tint = a[3] is { } fn ? FunctionTint.Create(doc, fn, names.Count, alt.ComponentCount, objNum) : null;

        return new PdfColorSpace(device ? PdfColorSpaceKind.DeviceN : PdfColorSpaceKind.Separation, names.Count)
        {
            BaseSpace = alt,
            Colorants = names,
            Tint = tint,
        };
    }
}
