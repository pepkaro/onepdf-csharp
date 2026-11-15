// SPDX-License-Identifier: MIT
// Independently authored generated regression controls; no upstream fixture or test source.
using System.Text;
using OnePdf.Content;

namespace OnePdf.Tests.Content;

public class DefaultColorSpaceTests
{
    private const string Paint = "0 0 10 10 re";

    // Only a parser fixture: deliberately not a usable colour-conversion profile.
    private static byte[] Profile(int n, byte marker = 1)
    {
        var bytes = new byte[128];
        Encoding.ASCII.GetBytes(n switch { 1 => "GRAY", 3 => "RGB ", _ => "CMYK" }).CopyTo(bytes, 16);
        Encoding.ASCII.GetBytes("acsp").CopyTo(bytes, 36);
        bytes[127] = marker;
        return bytes;
    }

    private static string Family(int n) => n switch { 1 => "Gray", 3 => "RGB", _ => "CMYK" };

    private static string Values(int n) => n switch { 1 => "0.2", 3 => "0.2 0.4 0.6", _ => "0.2 0.4 0.6 0.8" };

    private static PdfColorSpaceKind DeviceKind(int n) => n switch
    {
        1 => PdfColorSpaceKind.DeviceGray,
        3 => PdfColorSpaceKind.DeviceRgb,
        _ => PdfColorSpaceKind.DeviceCmyk,
    };

    private static void AssertProfile(PdfColorSpace? space, int n, byte marker = 1)
    {
        Assert.NotNull(space);
        Assert.Equal(PdfColorSpaceKind.IccBased, space.Kind);
        Assert.Equal(n, space.ComponentCount);
        Assert.Equal(Profile(n, marker), space.IccProfile.ToArray());
    }

    private sealed class Capture : PdfDevice
    {
        public List<PdfColor> Fills { get; } = [];
        public List<PdfColor> Strokes { get; } = [];
        public List<PdfImage> Images { get; } = [];
        public List<PdfShading> Shadings { get; } = [];
        public override void FillPath(in PdfPath path, bool evenOdd, in PdfGraphicsState state) => Fills.Add(state.FillColor);
        public override void StrokePath(in PdfPath path, in PdfGraphicsState state) => Strokes.Add(state.StrokeColor);
        public override void DrawImage(in PdfImage image, in PdfGraphicsState state) => Images.Add(image);
        public override void PaintShading(PdfShading shading, in PdfGraphicsState state) => Shadings.Add(shading);
    }

    private static Capture Interpret(PdfDocument doc)
    {
        var capture = new Capture();
        doc.Pages[0].Interpret(capture);
        return capture;
    }

    [Theory]
    [InlineData(1, "g", "G")]
    [InlineData(3, "rg", "RG")]
    [InlineData(4, "k", "K")]
    public void ShorthandNamedAndAliasedOperatorsUseTheDefault(int n, string fill, string stroke)
    {
        string family = Family(n), values = Values(n);
        string content = $"{values} {fill} {Paint} f {values} {stroke} {Paint} S "
            + $"/Device{family} cs {values} sc {Paint} f /Device{family} CS {values} SC {Paint} S "
            + $"/Alias cs {values} scn {Paint} f /Alias CS {values} SCN {Paint} S";
        using PdfDocument doc = TestPdf.Open(TestPdf.Build(content,
            $"/Resources << /ColorSpace << /Default{family} /Profile /Profile [/ICCBased 5 0 R] /Alias /Device{family} >> >>",
            b => b.Stream(5, $"/N {n} /Alternate /Device{family}", Profile(n)), objects: 6));
        Capture capture = Interpret(doc);
        Assert.Equal(3, capture.Fills.Count);
        Assert.Equal(3, capture.Strokes.Count);
        foreach (PdfColor color in capture.Fills.Concat(capture.Strokes))
        {
            AssertProfile(color.Space, n);
            Assert.Equal(0.2, color.C0, 10);
            if (n > 1)
            {
                Assert.Equal(0.4, color.C1, 10);
                Assert.Equal(0.6, color.C2, 10);
            }
            if (n == 4)
            {
                Assert.Equal(0.8, color.C3, 10);
            }
        }
        Assert.Empty(doc.Diagnostics);
    }

    [Theory]
    [InlineData(1, "G")]
    [InlineData(3, "RGB")]
    [InlineData(4, "CMYK")]
    public void XObjectAndInlineImagesUseTheDefaultWithoutChangingSamples(int n, string abbreviation)
    {
        byte[] samples = Enumerable.Range(1, n).Select(i => (byte)(i * 30)).ToArray();
        using PdfDocument doc = TestPdf.Open(TestPdf.Build($"/Im Do BI /W 1 /H 1 /BPC 8 /CS /{abbreviation} ID {Encoding.Latin1.GetString(samples)} EI",
            $"/Resources << /ColorSpace << /Default{Family(n)} [/ICCBased 5 0 R] >> /XObject << /Im 6 0 R >> >>",
            b => b.Stream(5, $"/N {n}", Profile(n)).Stream(6,
                $"/Type /XObject /Subtype /Image /Width 1 /Height 1 /BitsPerComponent 8 /ColorSpace /Device{Family(n)}", samples), objects: 7));
        Capture capture = Interpret(doc);
        Assert.Equal(2, capture.Images.Count);
        foreach (PdfImage image in capture.Images)
        {
            PdfImageData data = Assert.IsType<PdfImageData>(image.GetData());
            AssertProfile(data.ColorSpace, n);
            Assert.Equal(samples, data.Samples.ToArray());
            Assert.Same(data, image.GetData());
        }
        Assert.Empty(doc.Diagnostics);
    }

    [Fact]
    public void ExplicitProfileIsNotReplacedAndInitialGrayUsesItsDefault()
    {
        using PdfDocument doc = TestPdf.Open(TestPdf.Build($"{Paint} f /Explicit cs 0.2 0.4 0.6 0.8 sc {Paint} f",
            "/Resources << /ColorSpace << /DefaultGray [/ICCBased 5 0 R] /DefaultCMYK [/ICCBased 6 0 R] /Explicit [/ICCBased 7 0 R] >> >>",
            b => b.Stream(5, "/N 1", Profile(1)).Stream(6, "/N 4", Profile(4, 2)).Stream(7, "/N 4 /Alternate /DeviceCMYK", Profile(4, 3)), objects: 8));
        Capture capture = Interpret(doc);
        AssertProfile(capture.Fills[0].Space, 1);
        AssertProfile(capture.Fills[1].Space, 4, 3);
        Assert.Equal(PdfColorSpaceKind.DeviceCmyk, capture.Fills[1].Space.Alternate!.Kind);
        Assert.Empty(doc.Diagnostics);
    }

    [Theory]
    [InlineData("", false, 0)]
    [InlineData("0.25 sc", false, 0.25)]
    [InlineData("0.25 scn", false, 0.25)]
    [InlineData("0.25 SC", true, 0.25)]
    [InlineData("0.25 SCN", true, 0.25)]
    public void InitialDeviceGrayUsesTheCurrentDefault(string color, bool stroke, double component)
    {
        using PdfDocument doc = TestPdf.Open(TestPdf.Build($"{color} {Paint} {(stroke ? "S" : "f")}",
            "/Resources << /ColorSpace << /DefaultGray [/ICCBased 5 0 R] >> >>",
            b => b.Stream(5, "/N 1", Profile(1)), objects: 6));
        Capture capture = Interpret(doc);
        PdfColor result = Assert.Single(stroke ? capture.Strokes : capture.Fills);
        AssertProfile(result.Space, 1);
        Assert.Equal(component, result.C0);
        Assert.Empty(doc.Diagnostics);
    }

    [Theory]
    [InlineData("/DeviceCMYK")]
    [InlineData("/Alias")]
    [InlineData("/ArrayAlias")]
    public void DeviceCmykSelectionKeepsItsBlackInitialComponents(string name)
    {
        using PdfDocument doc = TestPdf.Open(TestPdf.Build($"{name} cs {Paint} f {name} CS {Paint} S /Direct cs {Paint} f",
            "/Resources << /ColorSpace << /DefaultCMYK [/ICCBased 5 0 R] /Alias /DeviceCMYK /ArrayAlias [/DeviceCMYK] /Direct [/ICCBased 5 0 R] >> >>",
            b => b.Stream(5, "/N 4 /Range [0 1 0 1 0 1 0 1]", Profile(4)), objects: 6));
        Capture capture = Interpret(doc);
        foreach (PdfColor result in new[] { capture.Fills[0], Assert.Single(capture.Strokes) })
        {
            AssertProfile(result.Space, 4);
            Assert.Equal(0, result.C0);
            Assert.Equal(0, result.C1);
            Assert.Equal(0, result.C2);
            Assert.Equal(1, result.C3);
        }
        // The explicit ICC space keeps its existing initialization policy; it is not a selected device space.
        AssertProfile(capture.Fills[1].Space, 4);
        Assert.Equal(0, capture.Fills[1].C3);
        Assert.Empty(doc.Diagnostics);
    }

    [Fact]
    public void InheritedDeviceColorRemapsInNestedFormsAndSurvivesSaveRestore()
    {
        using PdfDocument doc = TestPdf.Open(TestPdf.Build($"0.25 sc /A Do {Paint} f q /Direct cs 0.75 sc {Paint} f Q {Paint} f q 0.5 g Q {Paint} f",
            "/Resources << /ColorSpace << /DefaultGray [/ICCBased 5 0 R] /Direct [/ICCBased 6 0 R] >> /XObject << /A 7 0 R >> >>", b =>
            {
                b.Stream(5, "/N 1", Profile(1, 1));
                b.Stream(6, "/N 1", Profile(1, 2));
                b.Stream(7, "/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << /ColorSpace << /DefaultGray [/ICCBased 6 0 R] >> /XObject << /B 8 0 R >> >>",
                    Encoding.ASCII.GetBytes($"{Paint} f /B Do {Paint} f"));
                b.Stream(8, "/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << >>", Encoding.ASCII.GetBytes($"{Paint} f"));
            }, objects: 9));
        Capture capture = Interpret(doc);
        Assert.Equal(7, capture.Fills.Count);
        AssertProfile(capture.Fills[0].Space, 1, 2);
        Assert.Equal(PdfColorSpaceKind.DeviceGray, capture.Fills[1].Space.Kind);
        AssertProfile(capture.Fills[2].Space, 1, 2);
        AssertProfile(capture.Fills[3].Space, 1, 1);
        AssertProfile(capture.Fills[4].Space, 1, 2);
        AssertProfile(capture.Fills[5].Space, 1, 1);
        AssertProfile(capture.Fills[6].Space, 1, 1);
        for (int i = 0; i < capture.Fills.Count; i++)
        {
            Assert.Equal(i == 4 ? 0.75 : 0.25, capture.Fills[i].C0);
        }
        Assert.Empty(doc.Diagnostics);
    }

    [Fact]
    public void DeviceInitialValuesPassThroughASeparationDefault()
    {
        using PdfDocument doc = TestPdf.Open(TestPdf.Build($"{Paint} f /DeviceGray cs {Paint} f 0 g {Paint} f",
            "/Resources << /ColorSpace << /DefaultGray [/Separation /Ink /DeviceRGB << /FunctionType 2 /Domain [0 1] /C0 [0.2 0.4 0.6] /C1 [0.8 0.7 0.5] /N 1 >>] >> >>"));
        Capture capture = Interpret(doc);
        Assert.Equal(3, capture.Fills.Count);
        foreach (PdfColor result in capture.Fills)
        {
            Assert.Equal(PdfColorSpaceKind.DeviceRgb, result.Space.Kind);
            Assert.Equal(0.2, result.C0, 10);
            Assert.Equal(0.4, result.C1, 10);
            Assert.Equal(0.6, result.C2, 10);
        }
    }

    [Fact]
    public void ExplicitIccRemainsExplicitInsideAFormWithAnotherDefault()
    {
        using PdfDocument doc = TestPdf.Open(TestPdf.Build("/Direct cs 0.25 sc /F Do",
            "/Resources << /ColorSpace << /Direct [/ICCBased 5 0 R] >> /XObject << /F 7 0 R >> >>", b =>
            {
                b.Stream(5, "/N 1", Profile(1, 1));
                b.Stream(6, "/N 1", Profile(1, 2));
                b.Stream(7, "/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << /ColorSpace << /DefaultGray [/ICCBased 6 0 R] >> >>",
                    Encoding.ASCII.GetBytes($"{Paint} f"));
            }, objects: 8));
        PdfColor result = Assert.Single(Interpret(doc).Fills);
        AssertProfile(result.Space, 1, 1);
        Assert.Equal(0.25, result.C0);
    }

    [Fact]
    public void DamagedExplicitIccFallbackDoesNotBecomeADeviceSelection()
    {
        using PdfDocument doc = TestPdf.Open(TestPdf.Build($"/Direct cs 0.2 0.4 0.6 0.8 sc {Paint} f",
            "/Resources << /ColorSpace << /Direct [/ICCBased 5 0 R] /DefaultCMYK [/ICCBased 6 0 R] >> >>",
            b => b.Stream(5, "/N 4", [0]).Stream(6, "/N 4", Profile(4)), objects: 7));
        PdfColor result = Assert.Single(Interpret(doc).Fills);
        Assert.Equal(PdfColorSpaceKind.DeviceCmyk, result.Space.Kind);
        Assert.Equal(0.8, result.C3);
        Assert.Contains(doc.Diagnostics, d => d.Message.Contains("profile is damaged", StringComparison.Ordinal));
    }

    [Fact]
    public void DefaultRangesClampTheUnchangedDeviceValues()
    {
        using PdfDocument doc = TestPdf.Open(TestPdf.Build($"0 0.4 0.6 1 k {Paint} f",
            "/Resources << /ColorSpace << /DefaultCMYK [/ICCBased 5 0 R] >> >>",
            b => b.Stream(5, "/N 4 /Range [0.1 0.9 0.1 0.9 0.1 0.9 0.1 0.9]", Profile(4)), objects: 6));
        PdfColor color = Assert.Single(Interpret(doc).Fills);
        AssertProfile(color.Space, 4);
        Assert.Equal(0.1, color.C0);
        Assert.Equal(0.4, color.C1);
        Assert.Equal(0.6, color.C2);
        Assert.Equal(0.9, color.C3);
    }

    [Theory]
    [InlineData(1, "[/Indexed /DeviceRGB 0 <000000>]")]
    [InlineData(3, "[/Lab << /WhitePoint [1 1 1] >>]")]
    [InlineData(3, "[/Pattern /DeviceRGB]")]
    [InlineData(4, "/DeviceRGB")]
    [InlineData(4, "[/ICCBased 5 0 R]")]
    [InlineData(4, "/Missing")]
    [InlineData(4, "/Loop")]
    [InlineData(4, "[]")]
    [InlineData(4, "42")]
    [InlineData(4, "[/Bogus]")]
    public void IncompatibleOrMalformedDefaultsFallBackWithBoundedDiagnostics(int n, string replacement)
    {
        using PdfDocument doc = TestPdf.Open(TestPdf.Build($"/Device{Family(n)} cs {Values(n)} sc {Paint} f",
            $"/Resources << /ColorSpace << /Default{Family(n)} {replacement} /Loop /Loop >> >>",
            b => b.Stream(5, "/N 3", Profile(3)), objects: 6));
        Assert.Equal(DeviceKind(n), Assert.Single(Interpret(doc).Fills).Space.Kind);
        Assert.Contains(doc.Diagnostics, d => d.Message.Contains("compatible default", StringComparison.Ordinal));
        Assert.InRange(doc.Diagnostics.Count, 1, 4);
    }

    [Theory]
    [InlineData("/DeviceCMYK")]
    [InlineData("[/DeviceCMYK]")]
    [InlineData("/Alias")]
    [InlineData("[/ICCBased 5 0 R]")]
    public void DeviceAndDamagedIccDefaultsDoNotRecurse(string replacement)
    {
        using PdfDocument doc = TestPdf.Open(TestPdf.Build($"0.2 0.4 0.6 0.8 k {Paint} f",
            $"/Resources << /ColorSpace << /DefaultCMYK {replacement} /Alias /DeviceCMYK >> >>",
            b => b.Stream(5, "/N 4 /Alternate /DeviceCMYK", [0]), objects: 6));
        Assert.Equal(PdfColorSpaceKind.DeviceCmyk, Assert.Single(Interpret(doc).Fills).Space.Kind);
        Assert.DoesNotContain(doc.Diagnostics, d => d.Message.Contains("cyclic", StringComparison.Ordinal));
    }

    [Fact]
    public void SpecialSpacesRemapTheirDeviceBaseOrSelectedAlternate()
    {
        using PdfDocument doc = TestPdf.Open(TestPdf.Build("",
            "/Resources << /ColorSpace << /DefaultCMYK [/ICCBased 5 0 R] "
            + "/P [/Pattern /DeviceCMYK] /I [/Indexed /DeviceCMYK 0 <336699CC>] "
            + "/S [/Separation /Ink /DeviceCMYK 6 0 R] /D [/DeviceN [/Ink] /DeviceCMYK 6 0 R] >> >>",
            b => b.Stream(5, "/N 4", Profile(4)).Obj(6, "<< /FunctionType 2 /Domain [0 1] /C0 [0 0 0 0] /C1 [1 1 1 1] /N 1 >>"), objects: 7));
        foreach (string name in new[] { "P", "I", "S", "D" })
        {
            PdfColorSpace space = Assert.IsType<PdfColorSpace>(PdfColorSpace.Resolve(doc, new PdfName(name), doc.Pages[0].Resources));
            AssertProfile(space.BaseSpace, 4);
        }
    }

    [Fact]
    public void DeviceNCanBeTheDefaultAndItsDeviceAlternateIsNotRemappedAgain()
    {
        using PdfDocument doc = TestPdf.Open(TestPdf.Build($"0.2 0.4 0.6 0.8 k {Paint} f",
            "/Resources << /ColorSpace << /DefaultCMYK [/DeviceN [/C /M /Y /K] /DeviceCMYK 5 0 R] >> >>",
            b => b.Stream(5, "/FunctionType 4 /Domain [0 1 0 1 0 1 0 1] /Range [0 1 0 1 0 1 0 1]", Encoding.ASCII.GetBytes("{ }")), objects: 6));
        PdfColor color = Assert.Single(Interpret(doc).Fills);
        Assert.Equal(PdfColorSpaceKind.DeviceCmyk, color.Space.Kind);
        Assert.Equal(0.2, color.C0, 10);
        Assert.Equal(0.8, color.C3, 10);
        Assert.Empty(doc.Diagnostics);
    }

    [Fact]
    public void SharedObjectsAreResolvedInEachFormResourceScopeAndOnReturn()
    {
        const string contents = "0.2 0.4 0.6 0.8 k 0 0 10 10 re f /Im Do /Sh sh /Pattern cs /P scn 0 0 10 10 re f";
        const string shared = "/XObject << /Im 7 0 R >> /Shading << /Sh 8 0 R >> /Pattern << /P 9 0 R >>";
        using PdfDocument doc = TestPdf.Open(TestPdf.Build("/A Do /B Do /C Do /A Do",
            "/Resources << /XObject << /A 10 0 R /B 11 0 R /C 12 0 R >> >>", b =>
            {
                b.Stream(5, "/N 4", Profile(4, 1));
                b.Stream(6, "/N 4", Profile(4, 2));
                b.Stream(7, "/Type /XObject /Subtype /Image /Width 1 /Height 1 /BitsPerComponent 8 /ColorSpace /DeviceCMYK", [20, 40, 60, 80]);
                // The same indirect colour array is also shared; its nested device space needs the current default.
                b.Obj(13, "[/Separation /Ink /DeviceCMYK << /FunctionType 2 /Domain [0 1] /C0 [0 0 0 0] /C1 [1 1 1 1] /N 1 >>]");
                b.Obj(8, "<< /ShadingType 2 /ColorSpace 13 0 R /Coords [0 0 10 0] /Function << /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 1 >> >>");
                b.Obj(9, "<< /PatternType 2 /Shading 8 0 R >>");
                b.Stream(10, $"/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << {shared} /ColorSpace << /DefaultCMYK [/ICCBased 5 0 R] >> >>", Encoding.ASCII.GetBytes(contents));
                b.Stream(11, $"/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << {shared} /ColorSpace << /DefaultCMYK [/ICCBased 6 0 R] >> >>", Encoding.ASCII.GetBytes(contents));
                b.Stream(12, $"/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << {shared} >>", Encoding.ASCII.GetBytes(contents));
            }, objects: 14));
        Capture capture = Interpret(doc);
        Assert.Equal(8, capture.Fills.Count);
        Assert.Equal(4, capture.Images.Count);
        Assert.Equal(4, capture.Shadings.Count);
        for (int i = 0; i < 4; i++)
        {
            PdfImageData image = Assert.IsType<PdfImageData>(capture.Images[i].GetData());
            PdfShadingPattern pattern = Assert.IsType<PdfShadingPattern>(capture.Fills[(i * 2) + 1].Pattern);
            PdfColorSpace[] spaces = [capture.Fills[i * 2].Space, image.ColorSpace!, capture.Shadings[i].ColorSpace.FinalSpace, pattern.Shading.ColorSpace.FinalSpace];
            foreach (PdfColorSpace space in spaces)
            {
                if (i == 2)
                {
                    Assert.Equal(PdfColorSpaceKind.DeviceCmyk, space.Kind);
                }
                else
                {
                    AssertProfile(space, 4, i == 1 ? (byte)2 : (byte)1);
                }
            }
        }
        Assert.Same(capture.Shadings[0], capture.Shadings[3]);
        Assert.Same(capture.Fills[1].Pattern, capture.Fills[7].Pattern);
        Assert.Empty(doc.Diagnostics);
    }

    [Fact]
    public void FormWithoutResourcesInheritsAndExplicitResourcesDoNotMerge()
    {
        using PdfDocument doc = TestPdf.Open(TestPdf.Build($"/F Do /Empty Do 0.2 0.4 0.6 0.8 k {Paint} f",
            "/Resources << /ColorSpace << /DefaultCMYK [/ICCBased 5 0 R] >> /XObject << /F 6 0 R /Empty 7 0 R >> >>",
            b => b.Stream(5, "/N 4", Profile(4))
                .Stream(6, "/Type /XObject /Subtype /Form /BBox [0 0 10 10]", Encoding.ASCII.GetBytes($"0.2 0.4 0.6 0.8 k {Paint} f"))
                .Stream(7, "/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << >>", Encoding.ASCII.GetBytes($"0.2 0.4 0.6 0.8 k {Paint} f")), objects: 8));
        Capture capture = Interpret(doc);
        AssertProfile(capture.Fills[0].Space, 4);
        Assert.Equal(PdfColorSpaceKind.DeviceCmyk, capture.Fills[1].Space.Kind);
        AssertProfile(capture.Fills[2].Space, 4);
    }
}
