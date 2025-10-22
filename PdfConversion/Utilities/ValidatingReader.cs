using System.Xml;
using System.Xml.Schema;

namespace PdfConversion.Utilities;

/// <summary>
/// XmlReader wrapper that validates XML content against an XmlSchemaSet while reading.
/// Provides detailed validation messages with line numbers and severity levels.
/// Based on Taxxor TDM's ValidatingReader implementation.
/// </summary>
public class ValidatingReader : XmlReader
{
    private readonly XmlReader _reader;
    private readonly XmlSchemaValidator _validator;
    private readonly Stack<string> _elementStack = new();
    private readonly HashSet<string> _ids = new();

    private readonly Func<(string, string), bool>? _includeElement;
    private readonly Func<(string, string), (string, string), string, string?>? _includeAttribute;

    private int _suspended = -1;

    /// <summary>
    /// List of validation messages collected during validation.
    /// Each entry contains element name, line number, position, severity, and message.
    /// </summary>
    public List<(string element, int line, int position, XmlSeverityType severity, string message)> ValidationMessages { get; } = new();

    /// <summary>
    /// Creates a ValidatingReader from XML string content
    /// </summary>
    /// <param name="xmlContent">XML content to validate</param>
    /// <param name="schemaSet">XmlSchemaSet to validate against</param>
    /// <param name="includeElement">Optional filter for elements to include in validation</param>
    /// <param name="includeAttribute">Optional filter for attributes to include in validation</param>
    public ValidatingReader(
        string xmlContent,
        XmlSchemaSet schemaSet,
        Func<(string name, string ns), bool>? includeElement = null,
        Func<(string name, string ns), (string name, string ns), string, string?>? includeAttribute = null)
    {
        var stringReader = new StringReader(xmlContent);
        var settings = new XmlReaderSettings { Async = true };
        var reader = XmlReader.Create(stringReader, settings);
        (_reader, _includeElement, _includeAttribute) = (reader, includeElement, includeAttribute);
        _validator = new XmlSchemaValidator(
            reader.NameTable,
            schemaSet,
            (IXmlNamespaceResolver)reader,
            XmlSchemaValidationFlags.ReportValidationWarnings)
        {
            LineInfoProvider = reader as IXmlLineInfo
        };
        _validator.ValidationEventHandler += ValidationHandler;
        _validator.Initialize();
    }

    /// <summary>
    /// Creates a ValidatingReader from an existing XmlReader
    /// </summary>
    public ValidatingReader(
        XmlReader reader,
        XmlSchemaSet schemaSet,
        Func<(string name, string ns), bool>? includeElement = null,
        Func<(string name, string ns), (string name, string ns), string, string?>? includeAttribute = null)
    {
        (_reader, _includeElement, _includeAttribute) = (reader, includeElement, includeAttribute);
        _validator = new XmlSchemaValidator(
            reader.NameTable,
            schemaSet,
            (IXmlNamespaceResolver)reader,
            XmlSchemaValidationFlags.ReportValidationWarnings)
        {
            LineInfoProvider = reader as IXmlLineInfo
        };
        _validator.ValidationEventHandler += ValidationHandler;
        _validator.Initialize();
    }

    private void ValidationHandler(object? sender, ValidationEventArgs args)
    {
        // Track validation message with element path and line info
        ValidationMessages.Add((
            _elementStack.Count > 0 ? _elementStack.Peek() : "Document",
            args.Exception.LineNumber,
            args.Exception.LinePosition,
            args.Severity,
            args.Message));
    }

    public override int AttributeCount => _reader.AttributeCount;
    public override string BaseURI => _reader.BaseURI;
    public override int Depth => _reader.Depth;
    public override bool EOF => _reader.EOF;
    public override bool IsEmptyElement => _reader.IsEmptyElement;
    public override string LocalName => _reader.LocalName;
    public override string NamespaceURI => _reader.NamespaceURI;
    public override XmlNameTable NameTable => _reader.NameTable;
    public override XmlNodeType NodeType => _reader.NodeType;
    public override string Prefix => _reader.Prefix;
    public override ReadState ReadState => _reader.ReadState;
    public override string Value => _reader.Value;

    public override string GetAttribute(int i) => _reader.GetAttribute(i);
    public override string? GetAttribute(string name) => _reader.GetAttribute(name);
    public override string? GetAttribute(string name, string? namespaceURI) => _reader.GetAttribute(name, namespaceURI);
    public override string? LookupNamespace(string prefix) => _reader.LookupNamespace(prefix);
    public override bool MoveToAttribute(string name) => _reader.MoveToAttribute(name);
    public override bool MoveToAttribute(string name, string? ns) => _reader.MoveToAttribute(name, ns);
    public override bool MoveToElement() => _reader.MoveToElement();
    public override bool MoveToFirstAttribute() => _reader.MoveToFirstAttribute();
    public override bool MoveToNextAttribute() => _reader.MoveToNextAttribute();

    public override bool Read()
    {
        if (!_reader.Read())
        {
            _validator.EndValidation();
            return false;
        }

        if (_reader.Depth >= (uint)_suspended)
        {
            if (_reader.NodeType == XmlNodeType.EndElement && _reader.Depth == _suspended)
                _suspended = -1;
            return true;
        }

        switch (_reader.NodeType)
        {
            case XmlNodeType.Element:
                _elementStack.Push(_reader.Name);
                string localName = _reader.LocalName;
                string ns = _reader.NamespaceURI;
                if (_includeElement != null && !_includeElement((localName, ns)))
                {
                    _suspended = _reader.Depth;
                    break;
                }
                _validator.ValidateElement(localName, ns, null);
                bool selfClosing = _reader.IsEmptyElement;
                while (_reader.MoveToNextAttribute())
                {
                    string? v = _reader.Value;
                    if (_reader.LocalName == "id" && !_ids.Add(v))
                        ValidationMessages.Add((
                            _elementStack.Peek(),
                            ((IXmlLineInfo)_reader).LineNumber,
                            ((IXmlLineInfo)_reader).LinePosition,
                            XmlSeverityType.Error,
                            $"Duplicate id {v}"));
                    if (_includeAttribute == null || (v = _includeAttribute((localName, ns), (_reader.LocalName, _reader.NamespaceURI), v)) != null)
                        _validator.ValidateAttribute(_reader.LocalName, _reader.NamespaceURI, v, null);
                }
                _reader.MoveToElement();
                _validator.ValidateEndOfAttributes(null);
                if (selfClosing)
                {
                    _validator.ValidateEndElement(null);
                    _elementStack.Pop();
                }
                break;
            case XmlNodeType.EndElement:
                _validator.ValidateEndElement(null);
                _elementStack.Pop();
                break;
            case XmlNodeType.Text:
                _validator.ValidateText(_reader.Value);
                break;
            case XmlNodeType.Whitespace:
            case XmlNodeType.SignificantWhitespace:
                _validator.ValidateWhitespace(_reader.Value);
                break;
        }
        return true;
    }

    public override async Task<bool> ReadAsync()
    {
        if (!await _reader.ReadAsync())
        {
            _validator.EndValidation();
            return false;
        }

        if (_reader.Depth >= (uint)_suspended)
        {
            if (_reader.NodeType == XmlNodeType.EndElement && _reader.Depth == _suspended)
                _suspended = -1;
            return true;
        }

        switch (_reader.NodeType)
        {
            case XmlNodeType.Element:
                _elementStack.Push(_reader.Name);
                string localName = _reader.LocalName;
                string ns = _reader.NamespaceURI;
                if (_includeElement != null && !_includeElement((localName, ns)))
                {
                    _suspended = _reader.Depth;
                    break;
                }
                _validator.ValidateElement(localName, ns, null);
                bool selfClosing = _reader.IsEmptyElement;
                while (_reader.MoveToNextAttribute())
                {
                    string? v = await _reader.GetValueAsync();
                    if (_reader.LocalName == "id" && !_ids.Add(v))
                        ValidationMessages.Add((
                            _elementStack.Peek(),
                            ((IXmlLineInfo)_reader).LineNumber,
                            ((IXmlLineInfo)_reader).LinePosition,
                            XmlSeverityType.Error,
                            $"Duplicate id {v}"));
                    if (_includeAttribute == null || (v = _includeAttribute((localName, ns), (_reader.LocalName, _reader.NamespaceURI), v)) != null)
                        _validator.ValidateAttribute(_reader.LocalName, _reader.NamespaceURI, v, null);
                }
                _reader.MoveToElement();
                _validator.ValidateEndOfAttributes(null);
                if (selfClosing)
                {
                    _validator.ValidateEndElement(null);
                    _elementStack.Pop();
                }
                break;
            case XmlNodeType.EndElement:
                _validator.ValidateEndElement(null);
                _elementStack.Pop();
                break;
            case XmlNodeType.Text:
                _validator.ValidateText(await _reader.GetValueAsync());
                break;
            case XmlNodeType.Whitespace:
            case XmlNodeType.SignificantWhitespace:
                _validator.ValidateWhitespace(await _reader.GetValueAsync());
                break;
        }
        return true;
    }

    public override bool ReadAttributeValue() => _reader.ReadAttributeValue();
    public override void ResolveEntity() => _reader.ResolveEntity();
    public override Task<string> GetValueAsync() => _reader.GetValueAsync();

    /// <summary>
    /// Validates the entire document synchronously
    /// </summary>
    public void Validate()
    {
        while (Read()) ;
    }

    /// <summary>
    /// Validates the entire document asynchronously
    /// </summary>
    public async Task ValidateAsync()
    {
        while (await ReadAsync()) ;
    }

    /// <summary>
    /// Gets an XmlSchemaSet for the specified schema URIs (without caching)
    /// </summary>
    public static (XmlSchemaSet? schemaSet, List<string> validationErrors) GetSchemaSetNoCache(
        XmlResolver resolver,
        params string[] schemas)
    {
        return GetSchemaSetInternal(resolver, false, schemas);
    }

    /// <summary>
    /// Gets an XmlSchemaSet for the specified schema URIs (with caching)
    /// </summary>
    public static (XmlSchemaSet? schemaSet, List<string> validationErrors) GetSchemaSet(
        XmlResolver resolver,
        params string[] schemas)
    {
        return GetSchemaSetInternal(resolver, true, schemas);
    }

    private static Dictionary<string, (XmlSchemaSet, List<string>)> _schemaSets = new();

    private static (XmlSchemaSet? schemaSet, List<string> validationErrors) GetSchemaSetInternal(
        XmlResolver resolver,
        bool useCache,
        params string[] schemas)
    {
        lock (_schemaSets)
        {
            string key = string.Join(";", schemas);
            if (useCache && _schemaSets.TryGetValue(key, out (XmlSchemaSet schemaSet, List<string> validationErrors) result))
                return result;

            List<string> validationErrors = new();
            try
            {
                XmlSchemaSet schemaSet = new XmlSchemaSet() { XmlResolver = resolver };
                foreach (string schemaUri in schemas)
                {
                    using XmlReader reader = Create(schemaUri, new XmlReaderSettings() { XmlResolver = resolver });
                    XmlSchema schema = XmlSchema.Read(reader, (s, e) => validationErrors.Add($"{schemaUri}: {e.Message}"))
                        ?? throw new XmlSchemaException($"Schema {schemaUri} could not be read");
                    schemaSet.Add(schema);
                }
                schemaSet.Compile();
                result = (schemaSet, validationErrors);
                if (useCache)
                    _schemaSets[key] = result;
            }
            catch (Exception e)
            {
                validationErrors.Add($"{e.GetType().Name}: {e.Message}");
                result = (null!, validationErrors);
            }
            return result;
        }
    }
}
