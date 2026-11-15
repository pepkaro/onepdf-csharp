// SPDX-License-Identifier: MIT
// Copyright (c) 2026 OnePDF contributors
using System.Globalization;
using System.Text;

namespace OnePdf.Tests.Shading;

/// <summary>Original, self-contained PDF programs for ISO 32000-1 8.7.3.1 and 11.6.7.</summary>
public class PatternEntryStateTests
{
    private const string PatternResources = "/Pattern << /P 6 0 R >>";
    private const string Paint = "/Pattern cs /P scn 0 0 80 60 re f";
    private const string Line = "0.7 0.2 0.4 RG 2 30 m 78 30 l S";
    private const string Cell = "/Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 80 60] /XStep 80 /YStep 60";
    private const string Form = "/Type /XObject /Subtype /Form /BBox [0 0 320 240]";

    [Fact]
    public void FormPatternUsesEntryWidthNotDefaultOrPaintTimeWidth()
    {
        using PdfDocument doc = Open("3 w /F Do", "9 w [1 3] 0 d " + Paint);
        PdfGraphicsState stroke = Stroke(Pattern(Read(doc).Fills.Single()));
        Assert.Equal(3, stroke.LineWidth);
        Assert.Empty(stroke.Dash.ToArray());
        Assert.Equal(PdfMatrix.Identity, stroke.Ctm);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void ExplicitCellWidthOverridesParentEntry(double width)
    {
        string number = width.ToString(CultureInfo.InvariantCulture);
        using PdfDocument doc = Open("7 w /F Do", "9 w " + Paint, number + " w " + Line);
        Assert.Equal(width, Stroke(Pattern(Read(doc).Fills.Single())).LineWidth);
    }

    [Fact]
    public void PagePatternUsesPageEntryAcrossContentsArray()
    {
        byte[] pdf = MakePdf("9 w [2 3] 1 d " + Paint, PatternResources,
            (6, Stream(Cell + " /Resources << >>", Line)),
            (7, Stream(string.Empty, "5 w 2 J")));
        // A /Contents array is one logical parent stream, not a new entry state per part.
        string text = Encoding.ASCII.GetString(pdf).Replace("/Contents 4 0 R", "/Contents [7 0 R 4 0 R]", StringComparison.Ordinal);
        using PdfDocument doc = PdfDocument.Open(Rebuild(text));
        PdfGraphicsState stroke = Stroke(Pattern(Read(doc).Fills.Single()));
        Assert.Equal(1, stroke.LineWidth);
        Assert.Equal(0, stroke.LineCap);
        Assert.Empty(stroke.Dash.ToArray());
    }

    [Fact]
    public void OtherNonTransparencyParametersComeFromFormEntry()
    {
        const string entry = "3 w 2 J 1 j 6 M [2 4] 1 d 7 i /AbsoluteColorimetric ri /Entry gs /F Do";
        const string late = "9 w 0 J 2 j 12 M [5 6] 3 d 25 i /Saturation ri /Late gs ";
        const string states = "/ExtGState << /Entry << /OP true /op true /OPM 1 /AIS true /TK false >> /Late << /OP false /op false /OPM 0 /AIS false /TK true >> >>";
        using PdfDocument doc = Open(entry, late + Paint, extraPage: states, extraForm: states);
        PdfGraphicsState stroke = Stroke(Pattern(Read(doc).Fills.Single()));
        Assert.Equal(3, stroke.LineWidth);
        Assert.Equal(2, stroke.LineCap);
        Assert.Equal(1, stroke.LineJoin);
        Assert.Equal(6, stroke.MiterLimit);
        Assert.Equal(new double[] { 2, 4 }, stroke.Dash.ToArray());
        Assert.Equal(1, stroke.DashPhase);
        Assert.Equal(7, stroke.Flatness);
        Assert.Equal("AbsoluteColorimetric", stroke.RenderingIntent);
        Assert.True(stroke.FillOverprint);
        Assert.True(stroke.StrokeOverprint);
        Assert.Equal(1, stroke.OverprintMode);
        Assert.True(stroke.AlphaIsShape);
        Assert.False(stroke.TextKnockout);
    }

    [Fact]
    public void SamePatternObjectAndMatrixDoNotReuseDifferentFormEntryStates()
    {
        using PdfDocument doc = Open("3 w /F Do 5 w /F Do 3 w /F Do", "9 w " + Paint + " " + Paint);
        Recorder paints = Read(doc);
        Assert.Equal(6, paints.Fills.Count);
        for (int i = 0; i < 3; i++)
        {
            PdfTilingPattern first = Pattern(paints.Fills[i * 2]);
            Assert.Same(first, Pattern(paints.Fills[(i * 2) + 1]));
            Assert.Equal(i == 1 ? 5 : 3, Stroke(first).LineWidth);
            Assert.Equal(Pattern(paints.Fills[0]).Matrix, first.Matrix);
        }

        Assert.NotSame(Pattern(paints.Fills[0]), Pattern(paints.Fills[2]));
        Assert.Same(Pattern(paints.Fills[0]), Pattern(paints.Fills[4]));
    }

    [Fact]
    public void CacheAlsoSeparatesNonWidthEntryParameters()
    {
        using PdfDocument doc = Open("3 w 0 J [] 0 d /F Do 2 J [4 2] 1 d /F Do", "0 J [] 0 d " + Paint);
        Recorder paints = Read(doc);
        PdfGraphicsState first = Stroke(Pattern(paints.Fills[0]));
        PdfGraphicsState second = Stroke(Pattern(paints.Fills[1]));
        Assert.Equal(0, first.LineCap);
        Assert.Empty(first.Dash.ToArray());
        Assert.Equal(2, second.LineCap);
        Assert.Equal(new double[] { 4, 2 }, second.Dash.ToArray());
        Assert.Equal(1, second.DashPhase);
    }

    [Fact]
    public void NestedFormsRestoreEachParentEntryAndPageEntry()
    {
        using PdfDocument doc = PdfDocument.Open(MakePdf("3 w /F Do " + Paint,
            "/XObject << /F 5 0 R >> " + PatternResources,
            (5, Stream(Form + " /Resources << /XObject << /Inner 7 0 R >> " + PatternResources + " >>", "7 w /Inner Do 9 w " + Paint)),
            (6, Stream(Cell + " /Resources << >>", Line)),
            (7, Stream(Form + " /Resources << " + PatternResources + " >>", "11 w " + Paint))));
        Assert.Equal(new double[] { 7, 3, 1 }, Read(doc).Fills.Select(s => Stroke(Pattern(s)).LineWidth).ToArray());
    }

    // ISO 32000-1 8.7.2 note 2 requires local pattern contexts. This is malformed-input robustness,
    // not evidence for conformance: a deferred cell must not accidentally acquire the later form state.
    [Fact]
    public void NonconformingCrossContextPatternRetainsItsSelectionForRobustness()
    {
        using PdfDocument doc = PdfDocument.Open(MakePdf("3 w /F Do", "/XObject << /F 5 0 R >>",
            (5, Stream(Form + " /Resources << " + PatternResources + " /XObject << /Inner 7 0 R >> >>", "/Pattern cs /P scn 7 w /Inner Do")),
            (6, Stream(Cell + " /Resources << >>", Line)),
            (7, Stream(Form + " /Resources << >>", "11 w 0 0 80 60 re f"))));
        Assert.Equal(3, Stroke(Pattern(Read(doc).Fills.Single())).LineWidth);
    }

    [Fact]
    public void NestedPatternUsesItsParentCellsEntryNotLaterChanges()
    {
        using PdfDocument doc = PdfDocument.Open(MakePdf("3 w /F Do", "/XObject << /F 5 0 R >>",
            (5, Stream(Form + " /Resources << " + PatternResources + " >>", "9 w " + Paint)),
            (6, Stream(Cell + " /Resources << /Pattern << /Inner 7 0 R >> >>", "7 w /Pattern cs /Inner scn 0 0 80 60 re f")),
            (7, Stream(Cell + " /Resources << >>", Line))));
        PdfTilingPattern outer = Pattern(Read(doc).Fills.Single());
        var cell = new Recorder();
        outer.Cell!.Replay(cell);
        Assert.Equal(3, Stroke(Pattern(cell.Fills.Single())).LineWidth);
    }

    [Fact]
    public void SaveRestoreDoesNotMutateEntryOrPoisonFollowingPaints()
    {
        using PdfDocument doc = Open("3 w /F Do", "q 7 w " + Paint + " Q 9 w " + Paint + " 0 0 m 10 10 l S");
        Recorder paints = Read(doc);
        Assert.Equal(new double[] { 3, 3 }, paints.Fills.Select(s => Stroke(Pattern(s)).LineWidth).ToArray());
        Assert.Equal(9, paints.Strokes.Single().LineWidth);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImplicitGroupResetsAlphaBlendAndMaskButKeepsOtherEntryState(bool explicitGroup)
    {
        const string states = "/ExtGState << /Outer << /ca .25 /CA .5 /BM /Multiply /AIS true /TK false /SMask << /S /Alpha /G 8 0 R >> >> >>";
        using PdfDocument doc = PdfDocument.Open(MakePdf("3 w /Outer gs /F Do", "/XObject << /F 5 0 R >> " + states,
            (5, Stream(Form + (explicitGroup ? " /Group << /S /Transparency /I true >>" : string.Empty) + " /Resources << " + PatternResources + " >>", "9 w " + Paint)),
            (6, Stream(Cell + " /Resources << >>", Line)),
            (8, Stream(Form + " /Group << /S /Transparency /I true >> /Resources << >>", "1 g 0 0 320 240 re f"))));
        Recorder paints = Read(doc);
        PdfGraphicsState outer = paints.Fills.Last();
        PdfGraphicsState stroke = Stroke(Pattern(outer));
        Assert.Equal(3, stroke.LineWidth);
        Assert.Equal(1, stroke.FillAlpha);
        Assert.Equal(1, stroke.StrokeAlpha);
        Assert.Equal("Normal", stroke.BlendMode);
        Assert.Equal(0, stroke.SoftMaskId);
        Assert.True(stroke.AlphaIsShape);
        Assert.False(stroke.TextKnockout);
        PdfGraphicsState composite = explicitGroup ? paints.Groups.Single() : outer;
        Assert.Equal(.25, composite.FillAlpha);
        Assert.Equal(.5, composite.StrokeAlpha);
        Assert.Equal("Multiply", composite.BlendMode);
        Assert.NotEqual(0, composite.SoftMaskId);
    }

    [Fact]
    public void UncoloredCellInheritsLineStateWhileTintStaysOnThePaint()
    {
        using PdfDocument doc = PdfDocument.Open(MakePdf("3 w 2 J /F Do", "/XObject << /F 5 0 R >>",
            (5, Stream(Form + " /Resources << " + PatternResources + " /ColorSpace << /Tint [/Pattern /DeviceRGB] >> >>", "9 w /Tint cs .2 .6 .8 /P scn 0 0 80 60 re f")),
            (6, Stream(Cell.Replace("/PaintType 1", "/PaintType 2", StringComparison.Ordinal) + " /Resources << >>", "2 30 m 78 30 l S"))));
        PdfGraphicsState outer = Read(doc).Fills.Single();
        PdfGraphicsState stroke = Stroke(Pattern(outer));
        Assert.Equal(3, stroke.LineWidth);
        Assert.Equal(2, stroke.LineCap);
        Assert.Equal(.2, outer.FillColor.C0);
        Assert.Equal(.6, outer.FillColor.C1);
        Assert.Equal(.8, outer.FillColor.C2);
    }

    [Fact]
    public void CellTextStateComesFromEntryIncludingOriginalType3Font()
    {
        const string font = "<< /Type /Font /Subtype /Type3 /Name /Original /FontBBox [0 0 1 1] /FontMatrix [1 0 0 1 0 0] /CharProcs << /A 8 0 R >> /Encoding << /Type /Encoding /Differences [65 /A] >> /FirstChar 65 /LastChar 65 /Widths [1] /Resources << >> >>";
        using PdfDocument doc = PdfDocument.Open(MakePdf("BT /Original 12 Tf 2 Tc 4 Tw 75 Tz 14 TL 1 Tr 3 Ts ET 3 w /F Do",
            "/XObject << /F 5 0 R >> /Font << /Original 7 0 R >>",
            (5, Stream(Form + " /Resources << " + PatternResources + " >>", "BT 5 Tc 6 Tw 100 Tz 20 TL 0 Tr 7 Ts ET " + Paint)),
            (6, Stream(Cell + " /Resources << >>", "0.7 0.2 0.4 RG BT T* (A) Tj ET")),
            (7, font),
            (8, Stream(string.Empty, "1 0 d0 0 0 m 1 1 l S"))));
        var cell = new Recorder();
        Pattern(Read(doc).Fills.Single()).Cell!.Replay(cell);
        PdfGlyphRun run = Assert.Single(cell.Text);
        Assert.Equal("Original", run.FontName);
        Assert.Equal(12, run.FontSize);
        Assert.Equal(2, run.CharSpacing);
        Assert.Equal(4, run.WordSpacing);
        Assert.Equal(.75, run.HorizontalScale);
        Assert.Equal(3, run.Rise);
        Assert.Equal(PdfTextRenderMode.Stroke, run.RenderMode);
        Assert.Equal(-14, run.TextMatrix.F);
    }

    [Fact]
    public void PatternInsideOriginalType3ProcedureUsesProcedureEntry()
    {
        const string font = "<< /Type /Font /Subtype /Type3 /FontBBox [0 0 80 60] /FontMatrix [1 0 0 1 0 0] /CharProcs << /A 8 0 R >> /Encoding << /Type /Encoding /Differences [65 /A] >> /FirstChar 65 /LastChar 65 /Widths [80] /Resources << /Pattern << /P 6 0 R >> >> >>";
        using PdfDocument doc = PdfDocument.Open(MakePdf("3 w BT /Original 1 Tf (A) Tj ET", "/Font << /Original 7 0 R >>",
            (6, Stream(Cell + " /Resources << >>", Line)),
            (7, font),
            (8, Stream(string.Empty, "80 0 d0 9 w " + Paint))));
        Assert.Equal(3, Stroke(Pattern(Read(doc).Fills.Single())).LineWidth);
    }

    [Fact]
    public void RepeatedEquivalentFormEntriesBuildOneCellIncludingEqualDashValues()
    {
        string page = string.Join(" ", Enumerable.Repeat("3 w [2 4] 1 d /F Do", 40));
        using PdfDocument doc = Open(page, "9 w " + Paint);
        Recorder paints = Read(doc);
        Assert.Equal(40, paints.Fills.Count);
        Assert.Single(paints.Fills.Select(paint => paint.FillColor.Pattern).Distinct());
        PdfTilingPattern first = Pattern(paints.Fills[0]);
        Assert.All(paints.Fills, paint => Assert.Same(first, Pattern(paint)));
        Assert.Equal(new double[] { 2, 4 }, Stroke(first).Dash.ToArray());
    }

    [Fact]
    public void ChangesOnlyToOuterTransparencyReuseTheCell()
    {
        const string states = "/ExtGState << /A << /ca .25 /CA .5 /BM /Multiply >> /B << /ca .75 /CA .8 /BM /Screen >> >>";
        using PdfDocument doc = Open("3 w /A gs /F Do /B gs /F Do", Paint, extraPage: states);
        Recorder paints = Read(doc);
        Assert.Same(Pattern(paints.Fills[0]), Pattern(paints.Fills[1]));
        Assert.Equal(.25, paints.Fills[0].FillAlpha);
        Assert.Equal(.75, paints.Fills[1].FillAlpha);
        Assert.Equal("Multiply", paints.Fills[0].BlendMode);
        Assert.Equal("Screen", paints.Fills[1].BlendMode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedOrLegacyInheritedResourcesStillBelongToTheCurrentStream(bool shared)
    {
        // 7.8.3 associates the current resource dictionary with the executing stream, even for the
        // obsolete page-resource fallback. Sharing the dictionary object does not share entry state.
        string resources = "/XObject << /F 5 0 R >> " + PatternResources;
        byte[] bytes = MakePdf("3 w /F Do " + Paint, resources,
            (5, Stream(Form + (shared ? " /Resources 9 0 R" : string.Empty), "9 w " + Paint)),
            (6, Stream(Cell + " /Resources << >>", Line)),
            (9, "<< " + resources + " >>"));
        if (shared)
        {
            string text = Encoding.ASCII.GetString(bytes).Replace("/Resources << " + resources + " >>", "/Resources 9 0 R", StringComparison.Ordinal);
            bytes = Rebuild(text);
        }

        using PdfDocument doc = PdfDocument.Open(bytes);
        Recorder paints = Read(doc);
        Assert.Equal(new double[] { 3, 1 }, paints.Fills.Select(paint => Stroke(Pattern(paint)).LineWidth).ToArray());
    }

    [Fact]
    public void NestedSoftMaskFormRestoresTheParentEntryAndResetsCellTransparency()
    {
        const string mask = "/ExtGState << /Mask << /ca .5 /BM /Multiply /SMask << /S /Alpha /G 8 0 R >> >> >>";
        using PdfDocument doc = PdfDocument.Open(MakePdf("3 w /Mask gs /F Do", "/XObject << /F 5 0 R >> " + mask,
            (5, Stream(Form + " /Resources << " + PatternResources + " >>", "11 w " + Paint)),
            (6, Stream(Cell + " /Resources << >>", Line)),
            (8, Stream(Form + " /Group << /S /Transparency /I true >> /Resources << /XObject << /Nested 9 0 R >> >>", "5 w /Nested Do")),
            (9, Stream(Form + " /Resources << " + PatternResources + " >>", "9 w " + Paint))));
        Recorder paints = Read(doc);
        Assert.Equal(new double[] { 5, 3 }, paints.Fills.Select(paint => Stroke(Pattern(paint)).LineWidth).ToArray());
        Assert.Equal(0, paints.Fills[0].SoftMaskId);
        Assert.NotEqual(0, paints.Fills[1].SoftMaskId);
        Assert.All(paints.Fills, paint =>
        {
            PdfGraphicsState cell = Stroke(Pattern(paint));
            Assert.Equal(1, cell.FillAlpha);
            Assert.Equal(1, cell.StrokeAlpha);
            Assert.Equal("Normal", cell.BlendMode);
            Assert.Equal(0, cell.SoftMaskId);
        });
    }

    [Fact]
    public void UnusedEntryDefersCacheKeyAndKeepsACopyOfTheOriginalState()
    {
        var live = new OnePdf.Content.GState { LineWidth = 3 };
        var entry = new OnePdf.Content.PatternEntryState(live);
        System.Reflection.FieldInfo key = typeof(OnePdf.Content.PatternEntryState).GetField("_key",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.NotNull(key);
        Assert.Null(key.GetValue(entry));
        live.LineWidth = 9;
        Assert.Equal(3, entry.CreateState().LineWidth);
        Assert.Null(key.GetValue(entry));
        _ = entry.GetHashCode();
        Assert.NotNull(key.GetValue(entry));
        Assert.Equal(new OnePdf.Content.PatternEntryState(new OnePdf.Content.GState { LineWidth = 3 }), entry);
    }

    private static PdfDocument Open(string page, string form, string cell = Line, string extraPage = "", string extraForm = "") =>
        PdfDocument.Open(MakePdf(page, "/XObject << /F 5 0 R >> " + extraPage,
            (5, Stream(Form + " /Resources << " + PatternResources + " " + extraForm + " >>", form)),
            (6, Stream(Cell + " /Resources << >>", cell))));

    private static PdfTilingPattern Pattern(PdfGraphicsState state) => Assert.IsType<PdfTilingPattern>(state.FillColor.Pattern);

    private static PdfGraphicsState Stroke(PdfTilingPattern pattern)
    {
        var recorder = new Recorder();
        Assert.NotNull(pattern.Cell);
        pattern.Cell.Replay(recorder);
        return Assert.Single(recorder.Strokes);
    }

    private static Recorder Read(PdfDocument doc)
    {
        var recorder = new Recorder();
        doc.Pages[0].Interpret(recorder);
        Assert.DoesNotContain(doc.Diagnostics, d => d.Severity == PdfSeverity.Error);
        return recorder;
    }

    // The complete fixture writer is original. No fixture, image or font asset is read from disk.
    private static byte[] MakePdf(string page, string resources, params (int Number, string Body)[] extra)
    {
        var objects = new SortedDictionary<int, string>
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 320 240] /Resources << " + resources + " >> /Contents 4 0 R >>",
            [4] = Stream(string.Empty, page),
        };
        foreach (var (number, body) in extra)
        {
            objects.Add(number, body);
        }

        return Serialize(objects);
    }

    private static string Stream(string dictionary, string content) =>
        "<< " + dictionary + " /Length " + Encoding.ASCII.GetByteCount(content).ToString(CultureInfo.InvariantCulture) + " >>\nstream\n" + content + "\nendstream";

    private static byte[] Rebuild(string pdf)
    {
        var objects = new SortedDictionary<int, string>();
        for (int number = 1; number <= 9; number++)
        {
            string start = number.ToString(CultureInfo.InvariantCulture) + " 0 obj\n";
            int at = pdf.IndexOf(start, StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }

            at += start.Length;
            int end = pdf.IndexOf("\nendobj", at, StringComparison.Ordinal);
            objects.Add(number, pdf[at..end]);
        }

        return Serialize(objects);
    }

    private static byte[] Serialize(SortedDictionary<int, string> objects)
    {
        var text = new StringBuilder("%PDF-1.7\n");
        int count = objects.Keys.Max() + 1;
        var offsets = new int[count];
        foreach (var pair in objects)
        {
            offsets[pair.Key] = text.Length;
            text.Append(CultureInfo.InvariantCulture, $"{pair.Key} 0 obj\n{pair.Value}\nendobj\n");
        }

        int xref = text.Length;
        text.Append(CultureInfo.InvariantCulture, $"xref\n0 {count}\n0000000000 65535 f \n");
        for (int i = 1; i < count; i++)
        {
            text.Append(CultureInfo.InvariantCulture, $"{offsets[i]:D10} 00000 {(offsets[i] == 0 ? 'f' : 'n')} \n");
        }

        text.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(text.ToString());
    }

    private sealed class Recorder : PdfDevice
    {
        public List<PdfGraphicsState> Fills { get; } = [];
        public List<PdfGraphicsState> Strokes { get; } = [];
        public List<PdfGraphicsState> Groups { get; } = [];
        public List<PdfGlyphRun> Text { get; } = [];

        public override void FillPath(in PdfPath path, bool evenOdd, in PdfGraphicsState state) => Fills.Add(state);
        public override void StrokePath(in PdfPath path, in PdfGraphicsState state) => Strokes.Add(state);
        public override void BeginGroup(in PdfGroupInfo group, in PdfGraphicsState state) => Groups.Add(state);
        public override void ShowGlyphRun(in PdfGlyphRun run, in PdfGraphicsState state) => Text.Add(run);
    }
}
