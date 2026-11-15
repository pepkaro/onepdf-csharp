// SPDX-License-Identifier: MIT
using OnePdf.FontData;
using OnePdf.PdfFonts;
using static OnePdf.Tests.PdfFonts.FontPdf;

namespace OnePdf.Tests.PdfFonts;

public class EmbeddedCMapInheritanceTests
{
    private const string Parent = "/CMapName /Parent def 1 begincodespacerange <00> <FF> endcodespacerange 3 begincidchar <31> 4 <32> 7 <33> 4 endcidchar";
    private const string Child = "/CMapName /Child def 1 begincidchar <32> 9 endcidchar";

    private static CMap Load(PdfDocument doc) => FontSupport.LoadCMap(new FontContext(doc, new NoFonts(), 10), (PdfStream)doc.GetObject(10)!, "test CMap")!;

    [Fact]
    public void DictionaryParentSuppliesCodespaceAndChildOverridesCid()
    {
        using PdfDocument doc = Open(b =>
        {
            b.Stream(10, "/UseCMap 11 0 R", Ascii(Child));
            b.Stream(11, string.Empty, Ascii(Parent));
        });
        CMap map = Load(doc);
        Assert.Single(map.CodespaceRanges);
        Assert.True(map.ReadCode([0x31], out uint code, out int length, out bool valid));
        Assert.True(valid);
        Assert.Equal(0x31u, code);
        Assert.Equal(1, length);
        Assert.Equal(4, map.ToCid(0x31, 1));
        Assert.Equal(9, map.ToCid(0x32, 1));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CyclesAndDepthAreTruncatedWithoutDiscardingChild(bool cycle)
    {
        using PdfDocument doc = Open(b =>
        {
            for (int i = 10; i <= 27; i++)
            {
                string parent = cycle ? (i == 10 ? "/UseCMap 11 0 R" : "/UseCMap 10 0 R") : i < 27 ? $"/UseCMap {i + 1} 0 R" : string.Empty;
                b.Stream(i, parent, Ascii(i == 10 ? Child : Parent));
            }
        });
        CMap map = Load(doc);
        int count = 0;
        for (CMap? m = map; m is not null; m = m.UseCMap) count++;
        Assert.Equal(cycle ? 2 : 16, count);
        Assert.Equal(9, map.ToCid(0x32, 1));
    }

    [Fact]
    public void AggregateByteBudgetRejectsParentAndKeepsChild()
    {
        using PdfDocument doc = Open(b =>
        {
            b.Stream(10, "/UseCMap 11 0 R", Ascii(Child));
            b.Stream(11, string.Empty, new byte[FontSupport.MaxCMapBytes]);
        });
        CMap map = Load(doc);
        Assert.Null(map.UseCMap);
        Assert.Equal(9, map.ToCid(0x32, 1));
    }

    [Theory]
    [InlineData("/UseCMap /DefinitelyUnknown", "", false)]
    [InlineData("/UseCMap /Identity-H", "", true)]
    [InlineData("/UseCMap 11 0 R", "/Identity-H usecmap", true)]
    public void NamedResolutionAndProgramPrecedenceArePreserved(string dictionary, string prefix, bool identity)
    {
        using PdfDocument doc = Open(b =>
        {
            b.Stream(10, dictionary, Ascii(prefix + " " + Child));
            b.Stream(11, string.Empty, Ascii(Parent));
        });
        CMap map = Load(doc);
        if (identity) Assert.Equal("Identity-H", map.UseCMap!.Name);
        else Assert.Null(map.UseCMap);
    }

    [Fact]
    public void ToUnicodeRuntimeInheritsParentAndChildOverride()
    {
        using PdfDocument doc = Open(b =>
        {
            b.Obj(10, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding /ToUnicode 11 0 R >>");
            b.Stream(11, "/UseCMap 12 0 R", Ascii("1 beginbfchar <42> <005A> endbfchar"));
            b.Stream(12, string.Empty, Ascii("1 begincodespacerange <00> <FF> endcodespacerange 2 beginbfchar <41> <0058> <42> <0059> endbfchar"));
        });
        Assert.Equal("XZ", Shown(Decode(Font(doc, 10), Ascii("AB"))));
    }

    [Fact]
    public void EncodingRuntimeRoutesInheritedCidsToGlyphsAndWidths()
    {
        byte[] ttf = TinyTrueType.Build(4, [new(3, 1, new Dictionary<int, int> { [65] = 1, [66] = 2, [67] = 3 })]);
        using PdfDocument doc = Open(b =>
        {
            b.Obj(10, "<< /Type /Font /Subtype /Type0 /BaseFont /T /Encoding 15 0 R /DescendantFonts [11 0 R] >>");
            b.Obj(11, "<< /Type /Font /Subtype /CIDFontType2 /BaseFont /T /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor 12 0 R /DW 1000 /W [4 [360] 7 [480] 9 [620]] /CIDToGIDMap 14 0 R >>");
            b.Obj(12, "<< /Type /FontDescriptor /FontName /T /Flags 4 /FontFile2 13 0 R >>");
            b.Stream(13, $"/Length1 {ttf.Length}", ttf);
            b.Stream(14, string.Empty, [0,0, 0,0, 0,0, 0,0, 0,1, 0,0, 0,0, 0,2, 0,0, 0,3]);
            b.Stream(15, "/UseCMap 16 0 R", Ascii(Child));
            b.Stream(16, string.Empty, Ascii(Parent));
        });
        List<FontGlyph> glyphs = Decode(Font(doc, 10), 0x31, 0x32, 0x31);
        Assert.Equal([4, 9, 4], glyphs.Select(g => g.Cid));
        Assert.Equal([1, 3, 1], glyphs.Select(g => g.Gid));
        Assert.Equal([0.36, 0.62, 0.36], glyphs.Select(g => g.Advance));
        Assert.All(glyphs, g => Assert.Equal(1, g.CodeLength));
        Assert.Equal(214.4, glyphs.Sum(g => g.Advance) * 160, 6);
    }
}
