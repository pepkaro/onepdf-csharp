// SPDX-License-Identifier: MIT
using OneImage;
using OneImage.Pdf;
using OnePdf;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text.Json;

string root = Path.GetFullPath(args[0]);
string label = args[1];
double dpi = args.Length > 2 ? double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 144;
var report = new List<object>();
var config = new ImageConfiguration().Add(new PdfFormat(new PdfRenderOptions { Dpi = dpi }));
foreach (string input in Directory.GetFiles(root, "*.pdf").Order())
{
    string name = Path.GetFileNameWithoutExtension(input);
    byte[] pdf = File.ReadAllBytes(input);
    using var doc = PdfDocument.Open(pdf);
    var capture = new Capture();
    doc.Pages[0].Interpret(capture);
    Image image = Image.Load(pdf, new LoadOptions { Configuration = config });
    Rgba32[] pixels = image.ToArray<Rgba32>(maxDegree: 1);
    byte[] rgba = new byte[pixels.Length * 4];
    for (int i = 0; i < pixels.Length; i++)
    {
        rgba[i * 4] = pixels[i].R; rgba[(i * 4) + 1] = pixels[i].G;
        rgba[(i * 4) + 2] = pixels[i].B; rgba[(i * 4) + 3] = pixels[i].A;
    }
    string path = Path.Combine(root, label + "-" + name + ".png");
    using (FileStream file = File.Create(path))
        Image.FromPixels<Rgba32>(pixels, image.Width, image.Height).Save(file, PngFormat.Instance);
    report.Add(new { name, image.Width, image.Height, rgbaSha256 = Convert.ToHexString(SHA256.HashData(rgba)), pngSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), capture.Events, diagnostics = doc.Diagnostics.Select(x => x.Message).ToArray() });
}
File.WriteAllText(Path.Combine(root, label + "-report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
var provenance = new
{
    runtime = RuntimeInformation.FrameworkDescription, dpi, renderingWorkers = 1,
    assemblies = new[] { typeof(Image).Assembly, typeof(PdfFormat).Assembly, typeof(PdfDocument).Assembly, typeof(OnePdf.Fonts.FontsPackage).Assembly }
        .Select(a => new { name = a.GetName().Name, path = a.Location, version = a.GetName().Version?.ToString(), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))) }).ToArray(),
};
File.WriteAllText(Path.Combine(root, label + "-provenance.json"), JsonSerializer.Serialize(provenance, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{label}: rendered {report.Count} original controls at {dpi} dpi, one worker");
sealed class Capture : PdfDevice
{
    public List<object> Events { get; } = [];
    private void Add(string type, PdfColorSpace? cs, double[]? values = null) => Events.Add(new { type, values, kind = cs?.Kind.ToString(), components = cs?.ComponentCount, profileBytes = cs?.IccProfile.Length, profileSha256 = cs is null ? null : Convert.ToHexString(SHA256.HashData(cs.IccProfile.Span)) });
    public override void FillPath(in PdfPath path, bool evenOdd, in PdfGraphicsState state) => Add("vector", state.FillColor.Space, [state.FillColor.C0, state.FillColor.C1, state.FillColor.C2, state.FillColor.C3]);
    public override void StrokePath(in PdfPath path, in PdfGraphicsState state) => Add("stroke", state.StrokeColor.Space, [state.StrokeColor.C0, state.StrokeColor.C1, state.StrokeColor.C2, state.StrokeColor.C3]);
    public override void DrawImage(in PdfImage image, in PdfGraphicsState state) => Add("image", image.GetData()?.ColorSpace);
}
