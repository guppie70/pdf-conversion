using System;
using System.Xml;
using System.Xml.Schema;

class Program
{
    static void Main()
    {
        var xmlContent = System.IO.File.ReadAllText("test-reference.xml");

        Console.WriteLine("Loading XHTML schema...");
        var schemaSet = new XmlSchemaSet();
        using (var reader = XmlReader.Create("http://www.w3.org/2002/08/xhtml/xhtml1-strict.xsd"))
        {
            var schema = XmlSchema.Read(reader, (s, e) => Console.WriteLine($"Schema error: {e.Message}"));
            schemaSet.Add(schema);
        }
        schemaSet.Compile();
        Console.WriteLine("Schema loaded.\n");

        Console.WriteLine("Validating XML with XmlSchemaValidator...");
        var settings = new XmlReaderSettings();
        using var xmlReader = XmlReader.Create(new System.IO.StringReader(xmlContent), settings);

        var validator = new XmlSchemaValidator(
            xmlReader.NameTable,
            schemaSet,
            (IXmlNamespaceResolver)xmlReader,
            XmlSchemaValidationFlags.ReportValidationWarnings);

        validator.LineInfoProvider = xmlReader as IXmlLineInfo;
        validator.ValidationEventHandler += (sender, args) =>
        {
            Console.WriteLine($"[{args.Severity}] Line {args.Exception.LineNumber}:{args.Exception.LinePosition} - {args.Message}");
        };

        validator.Initialize();

        while (xmlReader.Read())
        {
            switch (xmlReader.NodeType)
            {
                case XmlNodeType.Element:
                    Console.WriteLine($"Processing element: {xmlReader.LocalName} at line {((IXmlLineInfo)xmlReader).LineNumber}");
                    validator.ValidateElement(xmlReader.LocalName, xmlReader.NamespaceURI, null);

                    if (xmlReader.MoveToFirstAttribute())
                    {
                        do
                        {
                            validator.ValidateAttribute(xmlReader.LocalName, xmlReader.NamespaceURI, xmlReader.Value, null);
                        } while (xmlReader.MoveToNextAttribute());
                        xmlReader.MoveToElement();
                    }

                    validator.ValidateEndOfAttributes(null);

                    if (xmlReader.IsEmptyElement)
                    {
                        validator.ValidateEndElement(null);
                    }
                    break;

                case XmlNodeType.EndElement:
                    validator.ValidateEndElement(null);
                    break;

                case XmlNodeType.Text:
                    validator.ValidateText(xmlReader.Value);
                    break;

                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    validator.ValidateWhitespace(xmlReader.Value);
                    break;
            }
        }

        validator.EndValidation();
        Console.WriteLine("\nValidation complete.");
    }
}
