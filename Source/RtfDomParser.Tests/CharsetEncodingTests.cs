using System.Text;
using NUnit.Framework;

namespace RtfDomParser.Tests
{
    /// <summary>
    /// Regression tests for the charset-encoding corruption fix in
    /// <see cref="RTFFontTable.GetRTFEncoding"/>: hex-escaped (\'XX) bytes must decode
    /// correctly regardless of whether the referenced font declares an explicit
    /// \fcharsetN, and independent of the .NET runtime's meaning of Encoding.Default.
    /// These tests are plain NUnit (no WinForms/MessageBox), unlike <see cref="RtfTest"/>.
    /// </summary>
    [TestFixture]
    public class CharsetEncodingTests
    {
        [SetUp]
        public void Setup()
        {
            Defaults.FontName = "Times New Roman";
        }

        /// <summary>
        /// Root cause 1 regression: a font-table entry without \fcharsetN defaults to
        /// charset 1 ("default"). Encoding.Default is UTF-8 on .NET Core/5+, which
        /// corrupts single-byte Windows-1252 hex escapes to U+FFFD. Must decode correctly.
        /// </summary>
        [Test]
        public void FontWithoutFcharset_DecodesHexEscapesCorrectly()
        {
            string rtf = @"{\rtf1\ansi\ansicpg1252{\fonttbl{\f0\fnil Arial;}}\f0 Rezept f\'fcr 4 Personen}";

            RTFDomDocument doc = new RTFDomDocument();
            doc.LoadRTFText(rtf);

            Assert.That(doc.InnerText.Trim(), Is.EqualTo("Rezept für 4 Personen"));
        }

        /// <summary>
        /// Root cause 2 regression: a font-table entry with explicit \fcharset0 ("ANSI")
        /// must decode the 0x80-0x9F byte range as Windows-1252 (e.g. \'80 -> '€'),
        /// not as Latin-1/control characters.
        /// </summary>
        [Test]
        public void FontWithFcharset0_DecodesEuroSignCorrectly()
        {
            string rtf = @"{\rtf1\ansi\ansicpg1252{\fonttbl{\f0\fnil\fcharset0 Arial;}}\f0 Preis: \'80 10}";

            RTFDomDocument doc = new RTFDomDocument();
            doc.LoadRTFText(rtf);

            Assert.That(doc.InnerText.Trim(), Is.EqualTo("Preis: € 10"));
        }

        /// <summary>
        /// No-regression check: document-level \ansicpg encoding resolution (no font table
        /// at all) was already correct before this fix and must remain so.
        /// </summary>
        [Test]
        public void NoFontTable_DocumentLevelAnsicpg_StillDecodesCorrectly()
        {
            string rtf = @"{\rtf1\ansi\ansicpg1252\pard f\'fcr \'80}";

            RTFDomDocument doc = new RTFDomDocument();
            doc.LoadRTFText(rtf);

            Assert.That(doc.InnerText.Trim(), Is.EqualTo("für €"));
        }

        /// <summary>
        /// Round-trip regression: writing a font table entry for a Windows-1252 encoding
        /// (which RTFFontTable.GetCharset resolves to charset 1, the "default" charset) and
        /// re-parsing the resulting RTF must preserve accented/hex-escaped characters,
        /// guarding against the GetCharset(Encoding) reverse-lookup or GetRTFEncoding
        /// falling back to a broken charset/encoding.
        /// </summary>
        [Test]
        public void RoundTrip_Windows1252Font_PreservesAccentedCharacters()
        {
            using System.IO.StringWriter sw = new System.IO.StringWriter();
            RTFDocumentWriter writer = new RTFDocumentWriter(sw);
            writer.CollectionInfo = false;
            writer.Writer.Encoding = Encoding.GetEncoding(1252);
            writer.FontTable.Add(0, "Arial", Encoding.GetEncoding(1252));

            writer.WriteStartDocument();
            writer.WriteStartParagraph();
            writer.Writer.WriteKeyword("f0");
            writer.WriteText("Rezept für 4 Personen: €10");
            writer.WriteEndParagraph();
            writer.WriteEndDocument();
            writer.Close();

            RTFDomDocument readDoc = new RTFDomDocument();
            readDoc.LoadRTFText(sw.ToString());

            Assert.That(readDoc.InnerText.Trim(), Is.EqualTo("Rezept für 4 Personen: €10"));
        }

        /// <summary>
        /// Regression for a fragility issue flagged in PR review: RTFFont/RTFFontTable must
        /// not depend on RTFDomDocument's or RTFWriter's static constructor having already run
        /// Defaults.LoadEncodings() (which registers CodePagesEncodingProvider). Constructing
        /// and using an RTFFont directly must not throw NotSupportedException when resolving
        /// charset 0/1 to Windows-1252. Note: because CodePagesEncodingProvider registration is
        /// process-wide, this test only reliably reproduces the original bug when run in
        /// isolation (e.g. via --filter), since other tests in the same run may already have
        /// triggered registration via RTFDomDocument/RTFWriter's static constructors.
        /// </summary>
        [Test]
        public void UsingRTFFontDirectly_WithoutTouchingRTFDomDocumentOrWriter_DoesNotThrow()
        {
            RTFFont font = new RTFFont(0, "Arial");

            Assert.That(() => font.Charset = 0, Throws.Nothing);
            Assert.That(font.Encoding?.WebName, Is.EqualTo("windows-1252"));
        }
    }
}
