<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns="http://www.w3.org/1999/xhtml"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                xmlns:x="adobe:ns:meta/"
                xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                exclude-result-prefixes="xs x rdf">

    <xsl:output method="xhtml"
                encoding="UTF-8"
                indent="yes"
                omit-xml-declaration="no"/>

    <xsl:template match="*" mode="#all" priority="-1">
        <xsl:element name="{local-name()}" namespace="http://www.w3.org/1999/xhtml">
            <!-- Copy attributes -->
            <xsl:apply-templates select="@*"/>
            <!-- Process children -->
            <xsl:apply-templates select="node()"/>
        </xsl:element>
    </xsl:template>

    <!-- Identity template for attributes: copy as-is!!! -->
    <xsl:template match="@*" mode="#all" priority="-1">
        <xsl:copy/>
    </xsl:template>

    <!-- Suppress xml:lang attributes - we don't need language metadata in the output -->
    <xsl:template match="@xml:lang" mode="#all" priority="0"/>

    <!-- Identity template for text nodes: normalize whitespace -->
    <xsl:template match="text()" mode="#all" priority="-1">
        <xsl:choose>
            <xsl:when test="normalize-space(.) = ''"/>
            <xsl:otherwise>
                <xsl:value-of select="normalize-space(.)"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <!-- Identity template for comments: copy through -->
    <xsl:template match="comment()" mode="#all" priority="-1">
        <xsl:copy/>
    </xsl:template>

    <!-- Identity template for processing instructions: copy through -->
    <xsl:template match="processing-instruction()" mode="#all" priority="-1">
        <xsl:copy/>
    </xsl:template>

    <!-- ============================================================ -->
    <!-- DOCUMENT STRUCTURE TEMPLATES                                 -->
    <!-- ============================================================ -->

    <!-- Root template: create XHTML wrapper -->
    <xsl:template match="/">
        <html>
            <head>
                <meta charset="UTF-8"/>
                <title>Taxxor TDM Document</title>
            </head>
            <body>
                <div class="document-content">
                    <xsl:apply-templates select="//Document"/>
                </div>
            </body>
        </html>
    </xsl:template>

    <!-- Document template: process children -->
    <xsl:template match="Document">
        <xsl:apply-templates/>
    </xsl:template>

    <!-- Suppress Adobe metadata elements -->
    <xsl:template match="x:xmpmeta | rdf:RDF" priority="10"/>

    <!-- Suppress bookmark tree -->
    <xsl:template match="bookmark-tree" priority="10"/>

    <!-- Suppress artifact elements only if empty (no attributes, no content) -->
    <xsl:template match="Artifact" priority="10">
        <xsl:if test="@* or normalize-space(.) != ''">
            <xsl:apply-templates/>
        </xsl:if>
    </xsl:template>

    <!-- Suppress processing instructions (xpacket) -->
    <xsl:template match="processing-instruction('xpacket')" priority="10"/>

    <!-- ============================================================ -->
    <!-- HEADER TRANSFORMATION TEMPLATES                              -->
    <!-- ============================================================ -->

    <xsl:template match="H1" priority="10">
        <h1>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </h1>
    </xsl:template>

    <xsl:template match="H2" priority="10">
        <h2>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </h2>
    </xsl:template>

    <xsl:template match="H3" priority="10">
        <h3>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </h3>
    </xsl:template>

    <!-- ============================================================ -->
    <!-- PARAGRAPH TRANSFORMATION TEMPLATES                           -->
    <!-- ============================================================ -->

    <xsl:template match="P" priority="10">
        <!-- Suppress empty or whitespace-only paragraphs to avoid extra newlines -->
        <xsl:if test="normalize-space(.) != ''">
            <p>
                <xsl:apply-templates select="@*"/>
                <xsl:apply-templates/>
            </p>
        </xsl:if>
    </xsl:template>

    <!-- ============================================================ -->
    <!-- TABLE TRANSFORMATION TEMPLATES                               -->
    <!-- ============================================================ -->
    <!-- Transform Adobe PDF tables to Taxxor TDM table structure     -->
    <!-- with required wrapper divs and metadata attributes           -->
    <!-- ============================================================ -->

    <xsl:template match="Table" priority="10">
        <xsl:variable name="tableId" select="generate-id()"/>
        <div id="tablewrapper_{$tableId}"
             class="table-wrapper structured-data-table"
             data-instanceid="{generate-id()}-wrapper">
            <div class="tablegraph-header-wrapper">
                <div class="table-title">tabletitle</div>
                <div class="table-scale">scale</div>
            </div>
            <table id="table_{$tableId}"
                   class="tabletype-numbers"
                   data-instanceid="{generate-id()}-table">
                <xsl:choose>
                    <!-- Header rows must contain ONLY TH elements (no TD elements) -->
                    <!-- Mixed rows (containing both TH and TD) are treated as body rows -->
                    <xsl:when test="TR[1][TH and not(TD)]">
                        <thead>
                            <!-- Header rows: only TR elements containing TH and no TD -->
                            <xsl:apply-templates select="TR[TH and not(TD)]" mode="header"/>
                        </thead>
                        <tbody>
                            <!-- Body rows: only TR elements containing at least one TD -->
                            <!-- This ensures no row appears in both thead and tbody -->
                            <xsl:apply-templates select="TR[TD]" mode="body"/>
                        </tbody>
                    </xsl:when>
                    <xsl:otherwise>
                        <tbody>
                            <xsl:apply-templates select="TR" mode="body"/>
                        </tbody>
                    </xsl:otherwise>
                </xsl:choose>
            </table>
        </div>
    </xsl:template>

    <!-- Table row templates in header mode -->
    <xsl:template match="TR" mode="header" priority="10">
        <tr>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates select="TH"/>
        </tr>
    </xsl:template>

    <!-- Table row templates in body mode -->
    <xsl:template match="TR" mode="body" priority="10">
        <tr>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates select="TH|TD"/>
        </tr>
    </xsl:template>

    <!-- Table header cell template -->
    <xsl:template match="TH" priority="10">
        <th>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </th>
    </xsl:template>

    <!-- Table data cell template -->
    <xsl:template match="TD" priority="10">
        <td>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </td>
    </xsl:template>

    <!-- Remove span elements from table cells (preserve content) -->
    <!-- This prevents numeric data from being wrapped in span elements -->
    <xsl:template match="TD/span | TH/span" priority="10">
        <xsl:apply-templates/>
    </xsl:template>

    <!-- ============================================================ -->
    <!-- LIST TRANSFORMATION TEMPLATES                                -->
    <!-- ============================================================ -->

    <!-- Deeply nested lists (5 levels) with single LI containing headings -->
    <!-- Handles two cases: -->
    <!-- 1. Simple: Just text in LBody → output as h3 -->
    <!-- 2. Nested: LBody has text followed by nested L with sub-items → h3 + h4 for each sub-item -->
    <xsl:template match="L[L[L[L[L[LI]]]]]" priority="30">
        <xsl:variable name="outerLBody" select="(.//LI)[1]/LBody"/>

        <xsl:choose>
            <!-- Case 2: LBody contains nested L elements (sub-items) -->
            <xsl:when test="$outerLBody/L">
                <!-- Extract text before the nested L (main heading) -->
                <xsl:variable name="mainText">
                    <xsl:for-each select="$outerLBody/text()">
                        <xsl:value-of select="normalize-space(.)"/>
                        <xsl:if test="position() != last()">
                            <xsl:text> </xsl:text>
                        </xsl:if>
                    </xsl:for-each>
                </xsl:variable>

                <!-- Output main heading as h3 -->
                <xsl:if test="normalize-space($mainText) != ''">
                    <h3>
                        <xsl:value-of select="normalize-space($mainText)"/>
                    </h3>
                </xsl:if>

                <!-- Output each nested sub-item as h4 with detected numberscheme -->
                <xsl:for-each select="$outerLBody/L/LI/LBody">
                    <xsl:variable name="subText" select="normalize-space(.)"/>
                    <xsl:if test="$subText != ''">
                        <h4>
                            <!-- Detect and add data-numberscheme attribute based on text pattern -->
                            <xsl:call-template name="add-numberscheme-attribute">
                                <xsl:with-param name="text" select="$subText"/>
                            </xsl:call-template>
                            <xsl:value-of select="$subText"/>
                        </h4>
                    </xsl:if>
                </xsl:for-each>
            </xsl:when>

            <!-- Case 1: Simple LBody with just text (original behavior) -->
            <xsl:otherwise>
                <xsl:variable name="text" select="normalize-space($outerLBody)"/>
                <h3>
                    <xsl:value-of select="$text"/>
                </h3>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <!-- Named template to detect numbering pattern and add data-numberscheme attribute -->
    <xsl:template name="add-numberscheme-attribute">
        <xsl:param name="text"/>
        <xsl:choose>
            <!-- Pattern: (i), (ii), (iii) - Roman numerals in parentheses -->
            <xsl:when test="matches($text, '^\([ivxlcdm]+\)\s')">
                <xsl:attribute name="data-numberscheme">(i),(ii),(iii)</xsl:attribute>
            </xsl:when>
            <!-- Pattern: (a), (b), (c) - Lowercase letters in parentheses -->
            <xsl:when test="matches($text, '^\([a-z]\)\s')">
                <xsl:attribute name="data-numberscheme">(a),(b),(c)</xsl:attribute>
            </xsl:when>
            <!-- Pattern: (A), (B), (C) - Uppercase letters in parentheses -->
            <xsl:when test="matches($text, '^\([A-Z]\)\s')">
                <xsl:attribute name="data-numberscheme">(A),(B),(C)</xsl:attribute>
            </xsl:when>
            <!-- Pattern: a., b., c. - Lowercase letters with period -->
            <xsl:when test="matches($text, '^[a-z]\.\s')">
                <xsl:attribute name="data-numberscheme">a.,b.,c.</xsl:attribute>
            </xsl:when>
            <!-- Pattern: A., B., C. - Uppercase letters with period -->
            <xsl:when test="matches($text, '^[A-Z]\.\s')">
                <xsl:attribute name="data-numberscheme">A.,B.,C.</xsl:attribute>
            </xsl:when>
            <!-- Pattern: 1., 2., 3. - Numbers with period -->
            <xsl:when test="matches($text, '^\d+\.\s')">
                <xsl:attribute name="data-numberscheme">1.,2.,3.</xsl:attribute>
            </xsl:when>
            <!-- No pattern detected: no attribute added -->
        </xsl:choose>
    </xsl:template>

    <!-- Numbered section headings (e.g., "1. Introduction") -->
    <xsl:template match="L[count(LI) = 1 and matches(normalize-space(LI/LBody), '^\d+\.\s+')]" priority="20">
        <xsl:variable name="text" select="normalize-space(LI/LBody)"/>
        <xsl:variable name="number" select="replace($text, '^(\d+\.)\s+.*', '$1')"/>
        <xsl:variable name="heading" select="replace($text, '^\d+\.\s+(.*)', '$1')"/>
        <h2 data-number="{$number}" data-numberscheme="1.,2.,3.">
            <xsl:value-of select="$heading"/>
        </h2>
    </xsl:template>

    <!-- General list template for regular lists -->
    <xsl:template match="L" priority="10">
        <xsl:choose>
            <xsl:when test="@ListType='Ordered'">
                <ol>
                    <xsl:apply-templates select="@* except @ListType"/>
                    <xsl:apply-templates/>
                </ol>
            </xsl:when>
            <xsl:otherwise>
                <ul>
                    <xsl:apply-templates select="@* except @ListType"/>
                    <xsl:apply-templates/>
                </ul>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <xsl:template match="LI" priority="10">
        <li>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </li>
    </xsl:template>

    <xsl:template match="Sect">
        <xsl:apply-templates/>
    </xsl:template>

    <xsl:template match="TOC">
        <ul>
            <xsl:apply-templates/>
        </ul>
    </xsl:template>
    
    <xsl:template match="TOCI" priority="10">
        <xsl:if test="normalize-space(.) != ''">
            <li>
                <xsl:apply-templates select="@*"/>
                <xsl:apply-templates/>
            </li>
        </xsl:if>
    </xsl:template>

    <xsl:template match="Reference">
        <a href="#">
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </a>
    </xsl:template>


</xsl:stylesheet>
