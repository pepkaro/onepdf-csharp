// SPDX-License-Identifier: MIT
// Copyright (c) 2026 OnePDF contributors
using OneImage;
using OneImage.Pdf;
using OnePdf;

Directory.CreateDirectory(args[1]);
foreach (string input in Directory.GetFiles(args[0], "*.pdf").Order())
{
    string output = Path.Combine(args[1], Path.GetFileNameWithoutExtension(input) + ".png");
    if (File.Exists(output)) throw new IOException("Refusing to overwrite an observed render.");
    var config = new ImageConfiguration().Add(new PdfFormat(new PdfRenderOptions
    {
        Dpi = 72, FirstPage = 0, PageCount = 1, Background = new Rgba32(255, 255, 255, 255),
    }));
    Image image = Image.Load(File.ReadAllBytes(input), new LoadOptions { Configuration = config });
    image.Save(output);
    using PdfDocument document = PdfDocument.Open(File.ReadAllBytes(input));
    var list = new PdfDisplayList();
    document.Pages[0].Interpret(list, new PdfInterpretOptions { Dpi = 72 });
    File.WriteAllText(Path.ChangeExtension(output, ".txt"), list.ToText());
    Console.WriteLine(Path.GetFileName(input));
}
