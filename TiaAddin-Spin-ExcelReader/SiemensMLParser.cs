﻿using SpinXmlReader.Block;
using SpinXmlReader.TagTable;
using System.Xml;
using TiaXmlReader.Utility;

namespace SpinXmlReader
{
    public static class SiemensMLParser
    {

        public static XmlNodeConfiguration ParseXML(XmlDocument document)
        {

            var tagTableNode = XMLUtils.GetFirstChild(document.DocumentElement, "SW.Tags.PlcTagTable");
            if (tagTableNode != null)
            {
                var tagTable = new XMLTagTable();
                tagTable.Parse(tagTableNode);
                return tagTable;
            }

            var blockFCNode = XMLUtils.GetFirstChild(document.DocumentElement, "SW.Blocks.FC");
            if (blockFCNode != null)
            {
                var blockFC  = new BlockFC();
                blockFC.Parse(blockFCNode);
                return blockFC;
            }

            var blockFBNode = XMLUtils.GetFirstChild(document.DocumentElement, "SW.Blocks.FB");
            if (blockFBNode != null)
            {
                var blockFB = new BlockFB();
                blockFB.Parse(blockFBNode);
                return blockFB;
            }

            /*
            var fcNode = XmlUtil.GetFirstChild(document, "SW.Blocks.FC");
            if (fcNode != null)
            {
                var fcData = new FCData(document);
                fcData.ParseXMLDocument();
                return;
            }

            var fbNode = XmlUtil.GetFirstChild(document, "SW.Blocks.FB");
            if (fcNode != null)
            {
                return;
            }*/

            return null;
        }

        public static XmlDocument CreateDocument()
        {
            var xmlDocument = new XmlDocument();
            xmlDocument.AppendChild(xmlDocument.CreateProcessingInstruction("xml", "version=\"1.0\" encoding =\"utf-8\""));

            XmlElement root = xmlDocument.CreateElement("Document");
            xmlDocument.AppendChild(root);

            var engineering = xmlDocument.CreateElement("Engineering");

            var versionAttribute = xmlDocument.CreateAttribute("version");
            versionAttribute.Value = Constants.VERSION;
            engineering.Attributes.Append(versionAttribute);

            root.AppendChild(engineering);

            return xmlDocument;
        }

        public static XMLTagTable CreateEmptyTagTable()
        {
            return new XMLTagTable();
        }

        public static BlockFC CreateEmptyFC()
        {
            var blockFC = new BlockFC();

            blockFC.ComputeBlockTitle().AddText(Constants.DEFAULT_CULTURE, "A Title? WOW!");
            blockFC.ComputeBlockComment().AddText(Constants.DEFAULT_CULTURE, "A Comment? WOW!");

            var blockAttributes = blockFC.GetBlockAttributes();
            var inputSection = blockAttributes.ComputeSection(SectionTypeEnum.INPUT);
            var outputSection = blockAttributes.ComputeSection(SectionTypeEnum.OUTPUT);
            var inOutSection = blockAttributes.ComputeSection(SectionTypeEnum.INOUT);
            var tempSection = blockAttributes.ComputeSection(SectionTypeEnum.TEMP);
            var constantSection = blockAttributes.ComputeSection(SectionTypeEnum.CONSTANT);

            var returnSection = blockAttributes.ComputeSection(SectionTypeEnum.RETURN);
            returnSection.AddReturnRetValMember();

            return blockFC;
        }
    }
}