using System.Linq;
using NUnit.Framework;

namespace RtfDomParser.Tests
{
    /// <summary>
    /// Regression tests for a text-merge bug in <see cref="RTFDomDocument.FixElements"/>:
    /// adjacent RTF brace groups ("{...}{...}") with byte-for-byte identical formatting were
    /// silently collapsed into a single <see cref="RTFDomText"/> node, discarding the RTF
    /// brace group's hard scope boundary and losing segment boundaries on round-trip.
    /// These tests are plain NUnit (no WinForms/MessageBox), unlike <see cref="RtfTest"/>.
    /// </summary>
    [TestFixture]
    public class BraceGroupMergeTests
    {
        [SetUp]
        public void Setup()
        {
            Defaults.FontName = "Arial";
        }

        /// <summary>
        /// Three sibling brace groups with identical formatting must remain three separate
        /// text nodes, not be merged into one because their formatting compares equal.
        /// </summary>
        [Test]
        public void SeparateBraceGroupsWithIdenticalFormatting_AreKeptAsSeparateNodes()
        {
            const string rtf =
                @"{\rtf1\ansi\deff0" +
                @"{\fonttbl{\f0\fswiss Arial;}}" +
                @"{\colortbl;\red0\green0\blue0;}" +
                @"\pard" +
                @"{\f0\fs16\cf0 Zubereitung }" +
                @"{\f0\fs16\cf0 Mehlsauce: }" +
                @"{\f0\fs16\cf0 70g Butter zerlaufen lassen}" +
                @"\par}";

            RTFDomDocument doc = new RTFDomDocument();
            doc.LoadRTFText(rtf);

            var textNodes = doc.Elements.OfType<RTFDomParagraph>()
                               .SelectMany(p => p.Elements.OfType<RTFDomText>())
                               .ToList();

            Assert.That(textNodes.Count, Is.EqualTo(3));
            Assert.That(textNodes[0].Text, Is.EqualTo("Zubereitung "));
            Assert.That(textNodes[1].Text, Is.EqualTo("Mehlsauce: "));
            Assert.That(textNodes[2].Text, Is.EqualTo("70g Butter zerlaufen lassen"));
        }

        /// <summary>
        /// When the first group uses different formatting (\cf1) than the other two (\cf0),
        /// all three groups must stay separate: the differing pair was already kept separate
        /// before the fix, but the still-\cf0-equal pair (groups 2 and 3) must now also stay
        /// separate because they come from different brace groups.
        /// </summary>
        [Test]
        public void BraceGroupsWithSameCfAsSiblingGroup_AreAllKeptSeparate()
        {
            const string rtf =
                @"{\rtf1\ansi\deff0" +
                @"{\fonttbl{\f0\fswiss Arial;}}" +
                @"{\colortbl;\red0\green0\blue0;}" +
                @"\pard" +
                @"{\f0\fs16\cf1 Zubereitung }" +
                @"{\f0\fs16\cf0 Mehlsauce: }" +
                @"{\f0\fs16\cf0 70g Butter zerlaufen lassen}" +
                @"\par}";

            RTFDomDocument doc = new RTFDomDocument();
            doc.LoadRTFText(rtf);

            var textNodes = doc.Elements.OfType<RTFDomParagraph>()
                               .SelectMany(p => p.Elements.OfType<RTFDomText>())
                               .ToList();

            Assert.That(textNodes.Count, Is.EqualTo(3));
            Assert.That(textNodes[0].Text, Is.EqualTo("Zubereitung "));
            Assert.That(textNodes[1].Text, Is.EqualTo("Mehlsauce: "));
            Assert.That(textNodes[2].Text, Is.EqualTo("70g Butter zerlaufen lassen"));
        }

        /// <summary>
        /// Sanity check: within a SINGLE brace group, a redundant inline formatting keyword
        /// (re-stating the same font/size, which does not actually change the resulting
        /// format) forces a mid-group text flush but must still coalesce back into a single
        /// text node, since both flushed runs share the same group and identical formatting.
        /// The fix must only prevent merging ACROSS group boundaries, not within the same group.
        /// </summary>
        [Test]
        public void RedundantInlineFormattingWithinSingleGroup_StillMergesIntoOneNode()
        {
            const string rtf =
                @"{\rtf1\ansi\deff0" +
                @"{\fonttbl{\f0\fswiss Arial;}}" +
                @"\pard" +
                @"{\f0\fs16 plain text\f0\fs16  still same format}" +
                @"\par}";

            RTFDomDocument doc = new RTFDomDocument();
            doc.LoadRTFText(rtf);

            var textNodes = doc.Elements.OfType<RTFDomParagraph>()
                               .SelectMany(p => p.Elements.OfType<RTFDomText>())
                               .ToList();

            Assert.That(textNodes.Count, Is.EqualTo(1));
            Assert.That(textNodes[0].Text, Is.EqualTo("plain text still same format"));
        }
    }
}
