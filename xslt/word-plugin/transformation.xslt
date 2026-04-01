<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="3.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:xs="http://www.w3.org/2001/XMLSchema"
    xmlns:local="http://taxxor.com/xslt/local"
    exclude-result-prefixes="xs local">

    <xsl:output method="xml" encoding="UTF-8" indent="no" omit-xml-declaration="yes"/>

    <xsl:strip-space elements="table thead tbody tr"/>

    <!-- Include modules -->
    <xsl:include href="modules/images.xslt"/>
    <xsl:include href="modules/cleanup.xslt"/>
    <xsl:include href="modules/tables.xslt"/>
    <xsl:include href="pass2/postprocess.xslt"/>
    <xsl:include href="pass3/table-symmetry.xslt"/>

    <!-- Base identity transforms for all passes -->
    <xsl:template match="*" mode="#all" priority="-1">
        <xsl:copy>
            <xsl:apply-templates select="@*" mode="#current"/>
            <xsl:apply-templates select="node()" mode="#current"/>
        </xsl:copy>
    </xsl:template>

    <xsl:template match="@*" mode="#all" priority="-1">
        <xsl:copy/>
    </xsl:template>

    <xsl:template match="text()" mode="#all" priority="-1">
        <xsl:copy/>
    </xsl:template>

    <!-- Suppress comments -->
    <xsl:template match="comment()" mode="#all" priority="-1"/>

    <!-- Root: 3-pass pipeline -->
    <xsl:template match="/">
        <!-- Pass 1: cleanup + table wrapping -->
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
                        <xsl:apply-templates select="/html/body/* | /body/*"/>
                    </div>
                </body>
            </html>
        </xsl:variable>

        <!-- Pass 2: asymmetry detection, title extraction -->
        <xsl:variable name="pass2-result">
            <xsl:apply-templates select="$pass1-result" mode="pass2"/>
        </xsl:variable>

        <!-- Pass 3: fix asymmetric tables -->
        <xsl:apply-templates select="$pass2-result" mode="pass3"/>
    </xsl:template>

    <!-- Strip source html/head/body structure (replaced by root template above) -->
    <xsl:template match="html | head | body | title | meta" priority="2">
        <xsl:apply-templates/>
    </xsl:template>

</xsl:stylesheet>
