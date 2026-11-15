// SPDX-License-Identifier: MIT
using System.Text;
using OnePdf.Content;

namespace OnePdf.Tests.Content;

/// <summary>Original numeric programs for corrected ISO 32000-2:2020 9.4.1.</summary>
public class TextMatrixSaveRestoreTests
{
    // Metrics only: these tests observe emitted matrices, not substituted outlines.
    private const string FontEntry = "/Font << /F << /Type /Font /Subtype /Type1 /BaseFont /OriginalMatrixMetrics /FirstChar 65 /LastChar 65 /Widths [500] >> >>";
    private const string Resources = "/Resources << " + FontEntry + " >>";

    private sealed class Capture : PdfDevice
    {
        public List<PdfMatrix> Matrices { get; } = [];

        public override void ShowGlyphRun(in PdfGlyphRun run, in PdfGraphicsState state) => Matrices.Add(run.TextMatrix);
    }

    private static byte[] Build(string content, string resources = Resources, Action<PdfBuilder>? extra = null, int objects = 5)
    {
        byte[] bytes = TestPdf.Build(content, resources, extra, objects: objects);
        "%PDF-2.0"u8.CopyTo(bytes);
        return bytes;
    }

    private static List<PdfMatrix> Matrices(byte[] bytes)
    {
        using PdfDocument doc = PdfDocument.Open(bytes);
        var capture = new Capture();
        doc.Pages[0].Interpret(capture);
        return capture.Matrices;
    }

    private static PdfMatrix At(double x, double y) => PdfMatrix.Translation(x, y);

    [Fact]
    public void FrozenOriginalPdfRestoresDifferentTextAndLineMatrices()
    {
        using Stream fixture = typeof(TextMatrixSaveRestoreTests).Assembly.GetManifestResourceStream("OriginalTextMatrixRestore.pdf")!;
        using PdfDocument doc = PdfDocument.Open(fixture);
        var capture = new Capture();
        doc.Pages[0].Interpret(capture);
        Assert.Equal([At(24, 120), At(146, 68), At(156, 48), At(64, 120), At(24, 86)], capture.Matrices);
    }

    [Fact]
    public void NestedSavesRestoreBothMatricesAndReuseGraphicsStates()
    {
        byte[] bytes = Build("BT /F 8 Tf 10 30 Td (A) Tj q 50 60 Td (A) Tj q 1 0 0 1 90 100 Tm (A) Tj Q (A) Tj 2 3 Td (A) Tj Q (A) Tj 0 -7 Td (A) Tj q 1 0 0 1 120 130 Tm Q (A) Tj ET");
        Assert.Equal([At(10, 30), At(60, 90), At(90, 100), At(64, 90), At(62, 93), At(14, 30), At(10, 23), At(14, 23)], Matrices(bytes));
    }

    [Fact]
    public void AffineMatricesAndTjAdjustmentsAreRestoredExactly()
    {
        byte[] bytes = Build("BT /F 8 Tf 2 1 -1 3 7 11 Tm [(A) -250] TJ q 1 0 0 1 80 90 Tm Q (A) Tj 2 -1 Td (A) Tj ET");
        Assert.Equal([new(2, 1, -1, 3, 7, 11), new(2, 1, -1, 3, 19, 17), new(2, 1, -1, 3, 12, 10)], Matrices(bytes));
    }

    [Fact]
    public void SavesOutsideTextDoNotCarryMatricesIntoLaterTextObjects()
    {
        byte[] bytes = Build("q BT /F 8 Tf 15 25 Td (A) Tj ET Q q BT /F 8 Tf (A) Tj q 1 0 0 1 90 90 Tm Q (A) Tj ET Q BT /F 8 Tf (A) Tj ET");
        Assert.Equal([At(15, 25), At(0, 0), At(4, 0), At(0, 0)], Matrices(bytes));
    }

    [Theory]
    [InlineData("ET BT")]
    [InlineData("BT")]
    public void MalformedCrossObjectRestoreCannotReviveOldTextMatrices(string boundary)
    {
        // Robustness policy, not a normative pixel oracle for improperly nested text objects.
        byte[] bytes = Build("BT /F 8 Tf 70 80 Td q " + boundary + " 4 6 Td Q (A) Tj 0 -2 Td (A) Tj ET");
        Assert.Equal([At(4, 6), At(4, 4)], Matrices(bytes));
    }

    [Fact]
    public void RestoreUnderflowRetainsTheCurrentMatrices()
    {
        byte[] bytes = Build("BT /F 8 Tf 13 17 Td Q (A) Tj 0 -3 Td (A) Tj ET");
        Assert.Equal([At(13, 17), At(13, 14)], Matrices(bytes));
    }

    [Fact]
    public void IgnoredOverDepthSaveAndRestoreDoNotConsumeAnHonouredSnapshot()
    {
        var content = new StringBuilder("BT /F 8 Tf 10 20 Td ");
        for (int i = 0; i < ContentInterpreter.MaxSaveDepth; i++)
        {
            content.Append("q ");
        }

        content.Append("q 1 0 0 1 70 80 Tm Q (A) Tj Q (A) Tj ");
        for (int i = 1; i < ContentInterpreter.MaxSaveDepth; i++)
        {
            content.Append("Q ");
        }

        content.Append("(A) Tj ET");
        Assert.Equal([At(70, 80), At(10, 20), At(10, 20)], Matrices(Build(content.ToString())));
    }

    [Fact]
    public void LiveInterpretationAndRepeatedDisplayListReplayAgree()
    {
        using PdfDocument doc = PdfDocument.Open(Build("BT /F 8 Tf 8 9 Td (A) Tj q 30 40 Td Q (A) Tj 0 -4 Td (A) Tj ET"));
        PdfDisplayList list = TestPdf.Record(doc);
        for (int pass = 0; pass < 2; pass++)
        {
            var live = new Capture();
            var replay = new Capture();
            doc.Pages[0].Interpret(live);
            list.Replay(replay);
            Assert.Equal([At(8, 9), At(12, 9), At(8, 5)], live.Matrices);
            Assert.Equal(live.Matrices, replay.Matrices);
        }
    }

    [Fact]
    public void ContentArrayPiecesShareTheSameTextObject()
    {
        var b = new PdfBuilder("2.0");
        b.Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
        b.Obj(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        b.Obj(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents [4 0 R 5 0 R] " + Resources + " >>");
        b.Stream(4, "", Encoding.ASCII.GetBytes("BT /F 8 Tf 3 7 Td (A) Tj q 90 90 Td"));
        b.Stream(5, "", Encoding.ASCII.GetBytes("Q (A) Tj 0 -2 Td (A) Tj ET"));
        b.Trailer("/Size 6 /Root 1 0 R", b.XrefTable(6));
        Assert.Equal([At(3, 7), At(7, 7), At(3, 5)], Matrices(b.ToArray()));
    }

    [Fact]
    public void RepeatedFormsKeepTheirOwnTextSnapshots()
    {
        byte[] bytes = Build("/X Do /X Do BT /F 8 Tf (A) Tj ET", "/Resources << " + FontEntry + " /XObject << /X 5 0 R >> >>", b =>
            b.Stream(5, "/Type /XObject /Subtype /Form /BBox [0 0 200 200]", Encoding.ASCII.GetBytes("BT /F 8 Tf 21 22 Td (A) Tj q 60 70 Td Q (A) Tj ET")), 6);
        Assert.Equal([At(21, 22), At(25, 22), At(21, 22), At(25, 22), At(0, 0)], Matrices(bytes));
    }

    [Fact]
    public void SoftMaskTextDoesNotInvalidateTheCallingTextObjectSnapshot()
    {
        byte[] bytes = Build("BT /F 8 Tf 9 12 Td (A) Tj q /Mask gs 70 80 Td Q (A) Tj 0 -3 Td (A) Tj ET",
            "/Resources << " + FontEntry + " /ExtGState << /Mask << /SMask << /S /Alpha /G 5 0 R >> >> >> >>", b =>
                b.Stream(5, "/Type /XObject /Subtype /Form /BBox [0 0 200 200] /Group << /S /Transparency /CS /DeviceGray >>", Encoding.ASCII.GetBytes("BT /F 8 Tf 40 50 Td q 10 10 Td Q (A) Tj ET")), 6);
        Assert.Equal([At(9, 12), At(40, 50), At(13, 12), At(9, 9)], Matrices(bytes));
    }

    [Fact]
    public void Type3TextDoesNotInvalidateTheCallingTextObjectSnapshot()
    {
        string resources = "/Resources << /Font << /F << /Type /Font /Subtype /Type1 /BaseFont /OriginalMatrixMetrics /FirstChar 65 /LastChar 65 /Widths [500] >> /T 5 0 R >> >>";
        byte[] bytes = Build("BT /F 8 Tf 9 12 Td (A) Tj q /T 10 Tf (X) Tj Q (A) Tj 0 -3 Td (A) Tj ET", resources, b =>
        {
            b.Obj(5, "<< /Type /Font /Subtype /Type3 /FontBBox [0 0 1000 1000] /FontMatrix [0.001 0 0 0.001 0 0] /FirstChar 88 /LastChar 88 /Widths [500] /Encoding << /Type /Encoding /Differences [88 /X] >> /CharProcs << /X 6 0 R >> /Resources << " + FontEntry + " >> >>");
            b.Stream(6, "", Encoding.ASCII.GetBytes("500 0 d0 BT /F 8 Tf 40 50 Td q 10 10 Td Q (A) Tj ET"));
        }, 7);
        Assert.Equal([At(9, 12), At(13, 12), At(40, 50), At(13, 12), At(9, 9)], Matrices(bytes));
    }

    [Fact]
    public void AnnotationResetDoesNotReusePageTextSnapshots()
    {
        byte[] bytes = Build("BT /F 8 Tf 9 12 Td q 50 60 Td Q (A) Tj ET", Resources + " /Annots [5 0 R 6 0 R]", b =>
        {
            b.Obj(5, "<< /Type /Annot /Subtype /Square /Rect [0 0 100 100] /AP << /N 7 0 R >> >>");
            b.Obj(6, "<< /Type /Annot /Subtype /Square /Rect [100 0 200 100] /AP << /N 7 0 R >> >>");
            b.Stream(7, "/Type /XObject /Subtype /Form /BBox [0 0 100 100] " + Resources,
                Encoding.ASCII.GetBytes("BT /F 8 Tf q 50 60 Td Q (A) Tj ET"));
        }, 8);
        Assert.Equal([At(9, 12), At(0, 0), At(0, 0)], Matrices(bytes));
    }
}
