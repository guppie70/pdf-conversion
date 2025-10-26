<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                xmlns:db="http://docbook.org/ns/docbook"
                exclude-result-prefixes="xs db">

    <!-- Output settings: XHTML5 serialization ensures proper handling of void vs non-void elements -->
    <xsl:output method="xhtml"
                encoding="UTF-8"
                indent="no"
                omit-xml-declaration="no"
                html-version="5.0"
                include-content-type="no"/>

    <!-- Strip whitespace-only text nodes from structural elements to keep output clean -->
    <xsl:strip-space elements="db:book db:chapter db:section table thead tbody tr"/>

    <!-- Parameter for Taxxor project ID (used for image path prefixing) -->
    <xsl:param name="projectid" select="'unknown'"/>

    <!-- Include modular XSLT files -->
    <xsl:include href="modules/tables.xslt"/>
    <xsl:include href="modules/headers.xslt"/>
    <xsl:include href="modules/images.xslt"/>
    <xsl:include href="pass2/postprocess.xslt"/>
    <xsl:include href="pass3/table-symmetry.xslt"/>

    <!-- Three-pass transformation:
         Pass 1 (Docling HTML → XHTML with table wrapping, default mode)
         Pass 2 (cleanup/normalization, mode="pass2" in pass2/postprocess.xslt)
         Pass 3 (table symmetry, mode="pass3" in pass3/table-symmetry.xslt) -->

    <!-- Identity transform base templates (mode="#all" - works in all passes) -->

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

    <xsl:template match="text()" mode="#all" priority="-1">
        <xsl:choose>
            <xsl:when test="normalize-space(.) = ''"/>
            <xsl:otherwise>
                <xsl:value-of select="normalize-space(.)"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <!-- Suppress comments -->
    <xsl:template match="comment()" mode="#all" priority="-1"/>

    <xsl:template match="processing-instruction()" mode="#all" priority="-1">
        <xsl:copy/>
    </xsl:template>

    <!-- Document structure templates - three-pass processing -->

    <xsl:template match="/">
        <!-- Pass 1: Docling HTML → Intermediate XHTML -->
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
                        <xsl:apply-templates select="//db:section | //db:chapter | /html/body | /body"/>
                    </div>
                </body>
            </html>
        </xsl:variable>

        <!-- Pass 2: Cleanup/normalization -->
        <xsl:variable name="pass2-result">
            <xsl:apply-templates select="$pass1-result" mode="pass2"/>
        </xsl:variable>

        <!-- Pass 3: Fix asymmetric tables by adding missing cells -->
        <xsl:apply-templates select="$pass2-result" mode="pass3"/>
    </xsl:template>

    <!-- Pass 1: DocBook structure unwrapping (default mode) -->

    <xsl:template match="db:book | db:chapter | db:section | db:info">
        <xsl:apply-templates/>
    </xsl:template>

    <!-- Pass 1: Standard HTML elements (already in correct format, just copy with attributes) -->

    <xsl:template match="h1 | h2 | h3 | h4 | h5 | h6" priority="10">
        <xsl:copy>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </xsl:copy>
    </xsl:template>

    <xsl:template match="p" priority="10">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:if test="$text != ''">
            <xsl:copy>
                <xsl:apply-templates select="@*"/>
                <xsl:apply-templates/>
            </xsl:copy>
        </xsl:if>
    </xsl:template>

    <xsl:template match="div | span | a | img | br" priority="10">
        <xsl:copy>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </xsl:copy>
    </xsl:template>

    <xsl:template match="ul | ol | li" priority="10">
        <xsl:copy>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </xsl:copy>
    </xsl:template>

    <!-- Pass 1: DocBook title elements → HTML headers -->

    <xsl:template match="db:title[parent::db:book]" priority="20">
        <h1>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </h1>
    </xsl:template>

    <xsl:template match="db:title[parent::db:chapter]" priority="20">
        <h2>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </h2>
    </xsl:template>

    <xsl:template match="db:title[parent::db:section]" priority="20">
        <h3>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </h3>
    </xsl:template>

</xsl:stylesheet>
