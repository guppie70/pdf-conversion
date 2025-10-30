<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                xmlns:hdr="http://taxxor.com/xslt/header-functions"
                xmlns:lst="http://taxxor.com/xslt/list-functions"
                xmlns:local="http://taxxor.com/xslt/local"
                xmlns:x="adobe:ns:meta/"
                xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                exclude-result-prefixes="xs hdr lst local x rdf">

    <!-- Output settings: XHTML5 serialization ensures proper handling of void vs non-void elements -->
    <xsl:output method="xhtml"
                encoding="UTF-8"
                indent="no"
                omit-xml-declaration="no"
                html-version="5.0"
                include-content-type="no"/>

    <!-- Strip whitespace-only text nodes from structural elements to keep output clean -->
    <xsl:strip-space elements="Document Sect Table TR thead tbody TOC L"/>

    <!-- Parameter for Taxxor project ID (used for image path prefixing) -->
    <xsl:param name="projectid" select="'unknown'"/>

    <!-- Include modular XSLT files -->
    <xsl:include href="modules/headers.xslt"/>
    <xsl:include href="modules/tables.xslt"/>
    <xsl:include href="modules/lists.xslt"/>
    <xsl:include href="pass2/postprocess.xslt"/>
    <xsl:include href="pass3/table-symmetry.xslt"/>
    <xsl:include href="pass4/tbody-normalize.xslt"/>
    <xsl:include href="pass5/strip-content.xslt"/>

    <!-- Five-pass transformation:
         Pass 1 (Adobe XML → XHTML, default mode)
         Pass 2 (cleanup, mode="pass2" in pass2/postprocess.xslt)
         Pass 3 (table symmetry, mode="pass3" in pass3/table-symmetry.xslt)
         Pass 4 (tbody normalization, mode="pass4" in pass4/tbody-normalize.xslt)
         Pass 5 (content stripping, mode="pass5" in pass5/strip-content.xslt) -->

    <!-- Identity transform base templates (mode="#all" - works in both passes) -->

    <xsl:template match="*" mode="#all" priority="-1">
        <xsl:element name="{local-name()}">
            <xsl:apply-templates select="@*" mode="#current"/>
            <xsl:apply-templates select="node()" mode="#current"/>
        </xsl:element>
    </xsl:template>

    <xsl:template match="@*" mode="#all" priority="-1">
        <xsl:copy/>
    </xsl:template>

    <xsl:template match="@xml:lang" mode="#all" priority="0"/>

    <!-- Preserve data-strip attributes through all passes (except pass5 which removes them) -->
    <xsl:template match="@data-strip" mode="pass2 pass3 pass4" priority="5">
        <xsl:copy/>
    </xsl:template>

    <xsl:template match="text()" mode="#all" priority="-1">
        <xsl:choose>
            <xsl:when test="normalize-space(.) = ''"/>
            <xsl:otherwise>
                <xsl:value-of select="normalize-space(.)"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <!-- Suppress comments (often contain duplicate/obsolete Adobe XML) -->
    <xsl:template match="comment()" mode="#all" priority="-1"/>

    <xsl:template match="processing-instruction()" mode="#all" priority="-1">
        <xsl:copy/>
    </xsl:template>

    <!-- Document structure templates - two-pass processing -->

    <xsl:template match="/">
        <!-- Pass 1: Adobe XML → Intermediate XHTML -->
        <xsl:variable name="pass1-result">
            <html>
                <head>
                    <meta charset="UTF-8"/>
                    <title>Taxxor TDM Document</title>

                    <style type="text/css">
                        /* Debug CSS: Display data-numberscheme values after headers */
                        h1[data-numberscheme]::after,
                        h2[data-numberscheme]::after,
                        h3[data-numberscheme]::after,
                        h4[data-numberscheme]::after {
                            content: " [scheme: " attr(data-numberscheme) "]";
                            color: #888;
                            font-size: 0.75em;
                            font-weight: normal;
                            font-style: italic;
                            margin-left: 0.5em;
                            opacity: 0.8;
                        }

                        /* Show data-number attribute */
                        h1[data-number]::before,
                        h2[data-number]::before,
                        h3[data-number]::before,
                        h4[data-number]::before {
                            content: "[" attr(data-number) "] ";
                            color: #999;
                            font-size: 0.75em;
                            font-weight: normal;
                            font-style: italic;
                            margin-right: 0.3em;
                            opacity: 0.7;
                        }

                        /* Highlight asymmetrical table rows */
                        tr[data-asymmetric="true"] {
                            border-top: 1px solid #ff9800;
                            border-bottom: 1px solid #ff9800;
                            border-left: 4px solid #ff9800;
                        }

                        tr[data-asymmetric="true"] td,
                        tr[data-asymmetric="true"] th {

                        }

                        /* Display cell count information after asymmetrical rows */
                        tr[data-asymmetric="true"]::after {
                            content: "⚠ Asymmetric row: " attr(data-cell-count) " cells (expected " attr(data-expected-count) ")";
                            display: block;
                            padding: 4px 8px;
                            background-color: #ff9800;
                            color: white;
                            font-size: 0.85em;
                            font-weight: bold;
                            text-align: center;
                            margin: 2px 0;
                        }

                        /* Highlight cells added by Pass 3 table symmetry fix */
                        td[data-cell-added="true"],
                        th[data-cell-added="true"] {
                            background-color: #e3f2fd;
                            border: 1px dashed #2196f3;
                        }

                        td[data-cell-added="true"]::after,
                        th[data-cell-added="true"]::after {
                            content: "➕";
                            color: #2196f3;
                            font-size: 0.75em;
                            margin-left: 0.25em;
                            opacity: 0.7;
                        }
                    </style>
                </head>
                <body>
                    <div class="document-content">
                        <xsl:apply-templates select="/TaggedPDF-doc/* | /Document"/>
                    </div>
                </body>
            </html>
        </xsl:variable>

        <!-- Pass 2: Cleanup (remove "(continued)", detect asymmetric rows) -->
        <xsl:variable name="pass2-result">
            <xsl:apply-templates select="$pass1-result" mode="pass2"/>
        </xsl:variable>

        <!-- Pass 3: Fix asymmetric tables by adding missing cells -->
        <xsl:variable name="pass3-result">
            <xsl:apply-templates select="$pass2-result" mode="pass3"/>
        </xsl:variable>

        <!-- Pass 4: Normalize tbody cells (convert <th> to <td>) -->
        <xsl:variable name="pass4-result">
            <xsl:apply-templates select="$pass3-result" mode="pass4"/>
        </xsl:variable>

        <!-- Pass 5: Remove content marked with data-strip="true" until data-strip="false" -->
        <xsl:apply-templates select="$pass4-result" mode="pass5"/>
    </xsl:template>

    <!-- Pass 1: Adobe XML to intermediate XHTML (default mode) -->

    <xsl:template match="Document|TaggedPDF-doc">
        <xsl:apply-templates/>
    </xsl:template>

    <xsl:template match="Sect|Part">
        <xsl:apply-templates/>
    </xsl:template>

    <!-- Pass 1: Suppression templates -->

    <xsl:template match="x:xmpmeta | rdf:RDF" priority="10"/>

    <xsl:template match="bookmark-tree" priority="10"/>

    <xsl:template match="Artifact" priority="10">
        <xsl:if test="@* or normalize-space(.) != ''">
            <xsl:apply-templates/>
        </xsl:if>
    </xsl:template>

    <xsl:template match="processing-instruction('xpacket')" priority="10"/>

    <!-- Pass 1: Paragraph transformation templates -->

    <!-- Strip report header fragments (priority=30) -->
    <xsl:template match="P[normalize-space(.) = 'Optiver Services BV | Annual Report 2024']" priority="30"/>

    <!-- P with nested P children: unwrap outer P, process children as siblings (priority=25) -->
    <xsl:template match="P[P]" priority="25">
        <!-- Process only P children and their siblings directly, skip wrapping -->
        <xsl:for-each select="node()[not(self::Reference)]">
            <xsl:choose>
                <xsl:when test="self::P">
                    <xsl:apply-templates select="."/>
                </xsl:when>
                <xsl:when test="self::text() and normalize-space(.) != ''">
                    <!-- Isolated text nodes between P elements become paragraphs -->
                    <p><xsl:value-of select="normalize-space(.)"/></p>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:apply-templates select="."/>
                </xsl:otherwise>
            </xsl:choose>
        </xsl:for-each>
    </xsl:template>

    <!-- P without nested P children: standard transformation (priority=10) -->
    <xsl:template match="P" priority="10">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:if test="$text != ''">
            <p>
                <xsl:apply-templates select="@*"/>
                <xsl:apply-templates/>
            </p>
        </xsl:if>
    </xsl:template>

    <!-- Pass 1: Link transformation templates -->

    <xsl:template match="Link">
        <a href="{.}">
            <xsl:apply-templates/>
        </a>
    </xsl:template>

    <xsl:template match="Reference">
        <a href="#">
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </a>
    </xsl:template>

    <!-- Pass 1: Figure transformation templates -->

    <xsl:template match="Figure" priority="10">
        <!-- Process all child elements (H5, ImageData, etc.) and text nodes -->
        <xsl:apply-templates select="*"/>
        <!-- Process any remaining text content as paragraph -->
        <xsl:variable name="text-content">
            <xsl:for-each select="text()">
                <xsl:value-of select="normalize-space(.)"/>
                <xsl:if test="position() != last()">
                    <xsl:text> </xsl:text>
                </xsl:if>
            </xsl:for-each>
        </xsl:variable>
        <xsl:if test="normalize-space($text-content) != ''">
            <p><xsl:value-of select="normalize-space($text-content)"/></p>
        </xsl:if>
    </xsl:template>

    <xsl:template match="ImageData" priority="10">
        <xsl:variable name="original-src" select="@src"/>
        <!-- Strip "images/" prefix if present, otherwise use original path -->
        <xsl:variable name="filename">
            <xsl:choose>
                <xsl:when test="starts-with($original-src, 'images/')">
                    <xsl:value-of select="substring-after($original-src, 'images/')"/>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:value-of select="$original-src"/>
                </xsl:otherwise>
            </xsl:choose>
        </xsl:variable>
        <!-- Construct Taxxor-compatible path -->
        <xsl:variable name="taxxor-path" select="concat('/dataserviceassets/{projectid}/images/from-conversion/', $filename)"/>
        <img src="{$taxxor-path}" alt=""/>
    </xsl:template>

</xsl:stylesheet>
