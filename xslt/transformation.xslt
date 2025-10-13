<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns="http://www.w3.org/1999/xhtml"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                xmlns:hdr="http://taxxor.com/xslt/header-functions"
                xmlns:lst="http://taxxor.com/xslt/list-functions"
                xmlns:x="adobe:ns:meta/"
                xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                exclude-result-prefixes="xs hdr lst x rdf">

    <xsl:output method="xhtml"
                encoding="UTF-8"
                indent="yes"
                omit-xml-declaration="no"/>

    <!-- Include modular XSLT files -->
    <xsl:include href="modules/headers.xslt"/>
    <xsl:include href="modules/tables.xslt"/>
    <xsl:include href="modules/lists.xslt"/>

    <!-- ============================================================ -->
    <!-- MULTI-PASS TRANSFORMATION STRATEGY                           -->
    <!-- ============================================================ -->
    <!-- This stylesheet uses a two-pass approach:                    -->
    <!-- Pass 1: Transform Adobe XML to intermediate XHTML            -->
    <!-- Pass 2: Post-process to remove "(continued)" elements        -->
    <!--                                                              -->
    <!-- Why multi-pass?                                              -->
    <!-- - "(continued)" appears in mixed content with Reference      -->
    <!-- - Text normalization happens during template processing      -->
    <!-- - Single-pass matching cannot reliably catch all patterns    -->
    <!-- - Post-processing on structured output is more reliable      -->
    <!-- ============================================================ -->

    <!-- ============================================================ -->
    <!-- IDENTITY TRANSFORM BASE TEMPLATES                            -->
    <!-- ============================================================ -->

    <xsl:template match="*" mode="#all" priority="-1">
        <xsl:element name="{local-name()}" namespace="http://www.w3.org/1999/xhtml">
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates select="node()"/>
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

    <xsl:template match="comment()" mode="#all" priority="-1">
        <xsl:copy/>
    </xsl:template>

    <xsl:template match="processing-instruction()" mode="#all" priority="-1">
        <xsl:copy/>
    </xsl:template>

    <!-- ============================================================ -->
    <!-- DOCUMENT STRUCTURE TEMPLATES - TWO-PASS PROCESSING           -->
    <!-- ============================================================ -->

    <xsl:template match="/">
        <!-- Pass 1: Transform Adobe XML to intermediate structure -->
        <xsl:variable name="pass1-result">
            <html>
                <head>
                    <meta charset="UTF-8"/>
                    <title>Taxxor TDM Document</title>

                    <!-- Debug visualization CSS for data-numberscheme attributes -->
                    <style type="text/css">
                        /* Display data-numberscheme attribute values after headers for debugging */
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

                        /* Optional: Show data-number attribute as well */
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
                    </style>
                </head>
                <body>
                    <div class="document-content">
                        <xsl:apply-templates select="//Document" mode="pass1"/>
                    </div>
                </body>
            </html>
        </xsl:variable>

        <!-- Pass 2: Post-process to remove "(continued)" elements -->
        <xsl:apply-templates select="$pass1-result" mode="pass2"/>
    </xsl:template>

    <!-- ============================================================ -->
    <!-- PASS 1: ADOBE XML TO INTERMEDIATE XHTML                      -->
    <!-- ============================================================ -->

    <xsl:template match="Document" mode="pass1">
        <xsl:apply-templates mode="pass1"/>
    </xsl:template>

    <xsl:template match="Sect" mode="pass1">
        <xsl:apply-templates mode="pass1"/>
    </xsl:template>

    <!-- ============================================================ -->
    <!-- PASS 1: SUPPRESSION TEMPLATES                                -->
    <!-- ============================================================ -->

    <xsl:template match="x:xmpmeta | rdf:RDF" mode="pass1" priority="10"/>

    <xsl:template match="bookmark-tree" mode="pass1" priority="10"/>

    <xsl:template match="Artifact" mode="pass1" priority="10">
        <xsl:if test="@* or normalize-space(.) != ''">
            <xsl:apply-templates mode="pass1"/>
        </xsl:if>
    </xsl:template>

    <xsl:template match="processing-instruction('xpacket')" mode="pass1" priority="10"/>

    <!-- ============================================================ -->
    <!-- PASS 1: PARAGRAPH TRANSFORMATION TEMPLATES                   -->
    <!-- ============================================================ -->

    <xsl:template match="P" mode="pass1" priority="10">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:if test="$text != ''">
            <p>
                <xsl:apply-templates select="@*"/>
                <xsl:apply-templates mode="pass1"/>
            </p>
        </xsl:if>
    </xsl:template>

    <!-- ============================================================ -->
    <!-- PASS 1: LINK TRANSFORMATION TEMPLATES                        -->
    <!-- ============================================================ -->

    <xsl:template match="Reference" mode="pass1">
        <a href="#">
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates mode="pass1"/>
        </a>
    </xsl:template>

    <!-- ============================================================ -->
    <!-- PASS 2: POST-PROCESSING TO REMOVE "(continued)" ELEMENTS     -->
    <!-- ============================================================ -->
    <!-- This pass removes any paragraph that:                        -->
    <!-- 1. Contains the text "(continued)" (case-insensitive)        -->
    <!-- 2. Has trailing/leading whitespace around "(continued)"      -->
    <!-- 3. Has "(continued)" in mixed content with links/elements    -->
    <!--                                                              -->
    <!-- Regex pattern matches:                                       -->
    <!-- - Optional whitespace before "(continued)"                   -->
    <!-- - Case variations: (continued), (Continued), (CONTINUED)     -->
    <!-- - Optional whitespace/punctuation after                      -->
    <!-- ============================================================ -->

    <!-- Identity transform for pass2 (copy everything by default) -->
    <xsl:template match="node() | @*" mode="pass2">
        <xsl:copy>
            <xsl:apply-templates select="node() | @*" mode="pass2"/>
        </xsl:copy>
    </xsl:template>

    <!-- Suppress paragraphs ending with "(continued)" in any form -->
    <xsl:template match="*[local-name()='p']" mode="pass2">
        <xsl:variable name="full-text" select="normalize-space(string(.))"/>

        <!-- Check if text matches "(continued)" pattern using regex -->
        <xsl:variable name="has-continued"
                      select="matches($full-text, '\(continued\)\s*$', 'i')"/>

        <!-- Only output the paragraph if it doesn't end with "(continued)" -->
        <xsl:if test="not($has-continued)">
            <xsl:copy>
                <xsl:apply-templates select="node() | @*" mode="pass2"/>
            </xsl:copy>
        </xsl:if>
    </xsl:template>

    <!-- Special handling for paragraphs containing ONLY "(continued)" -->
    <xsl:template match="*[local-name()='p'][normalize-space(.) = '(continued)']"
                  mode="pass2"
                  priority="15"/>

</xsl:stylesheet>
