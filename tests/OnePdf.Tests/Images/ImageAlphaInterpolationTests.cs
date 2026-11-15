// SPDX-License-Identifier: MIT
// Copyright (c) 2026 OnePDF contributors
using static OnePdf.Tests.Images.ImageKit;

namespace OnePdf.Tests.Images;

/// <summary>Decoded opacity retains the interpolation flag of the image supplying its samples.</summary>
public class ImageAlphaInterpolationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SoftMaskRetainsItsOwnFlag(bool parent, bool mask)
    {
        using Result r = Make($"/Width 3 /Height 1 /BitsPerComponent 8 /ColorSpace /DeviceGray /Interpolate {Flag(parent)} /SMask 11 0 R", [37, 113, 199], null, null,
            new Obj(11, $"/Type /XObject /Subtype /Image /Width 5 /Height 1 /BitsPerComponent 8 /ColorSpace /DeviceGray /Decode [1 0] /Interpolate {Flag(mask)}", [0, 53, 127, 211, 255]));
        Assert.Equal(parent, r.Img.Interpolate);
        Assert.Equal(mask, r.Img.Alpha!.Interpolate);
        Assert.Equal(new byte[] { 255, 202, 128, 44, 0 }, Alpha(r.Img));
        Assert.Equal(5, r.Img.Alpha.Width);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ExplicitStencilRetainsItsOwnFlag(bool parent, bool mask)
    {
        using Result r = Make($"/Width 3 /Height 1 /BitsPerComponent 8 /ColorSpace /DeviceGray /Interpolate {Flag(parent)} /Mask 11 0 R", [37, 113, 199], null, null,
            new Obj(11, $"/Type /XObject /Subtype /Image /Width 5 /Height 1 /ImageMask true /BitsPerComponent 1 /Interpolate {Flag(mask)}", [0x50]));
        Assert.Equal(mask, r.Img.Alpha!.Interpolate);
        Assert.Equal(new byte[] { 255, 0, 255, 0, 255 }, Alpha(r.Img));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ColorKeyAndStandaloneStencilUseTheirImageFlag(bool flag)
    {
        using Result key = Make($"/Width 3 /Height 1 /BitsPerComponent 8 /ColorSpace /DeviceGray /Interpolate {Flag(flag)} /Mask [100 120]", [37, 113, 199]);
        Assert.Equal(flag, key.Img.Alpha!.Interpolate);
        using Result stencil = Make($"/Width 5 /Height 1 /ImageMask true /Interpolate {Flag(flag)}", [0x50]);
        Assert.Equal(flag, stencil.Img.Alpha!.Interpolate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmbeddedJpxAlphaUsesItsImageFlag(bool flag)
    {
        var decoder = new FakeDecoder(_ => new PdfDecodedImage(2, 1, 1, 8, new byte[] { 37, 91, 199, 173 }) { HasAlpha = true });
        using Result r = Make($"/Width 2 /Height 1 /Interpolate {Flag(flag)} /SMaskInData 1 /Filter /JPXDecode", [1], With("JPXDecode", decoder));
        Assert.Equal(flag, r.Img.Alpha!.Interpolate);
        Assert.Equal(new byte[] { 91, 173 }, Alpha(r.Img));
    }

    [Fact]
    public void OmittedMaskFlagDefaultsToFalseRatherThanInheritingParent()
    {
        using Result r = Make("/Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Interpolate true /SMask 11 0 R", [37], null, null,
            new Obj(11, "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8", [173]));
        Assert.False(r.Img.Alpha!.Interpolate);
    }

    private static string Flag(bool value) => value ? "true" : "false";
}
