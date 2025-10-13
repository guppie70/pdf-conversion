<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns="http://www.w3.org/1999/xhtml"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                xmlns:hdr="http://taxxor.com/xslt/header-functions"
                xmlns:x="adobe:ns:meta/"
                xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                exclude-result-prefixes="xs hdr x rdf">

    <xsl:output method="xhtml"
                encoding="UTF-8"
                indent="yes"
                omit-xml-declaration="no"/>

    <!-- Include modular XSLT files -->
    <xsl:include href="modules/headers.xslt"/>
    <xsl:include href="modules/tables.xslt"/>
    <xsl:include href="modules/lists.xslt"/>

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
    <!-- DOCUMENT STRUCTURE TEMPLATES                                 -->
    <!-- ============================================================ -->

    <xsl:template match="/">
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
                    <xsl:apply-templates select="//Document"/>
                </div>
            </body>
        </html>
    </xsl:template>

    <xsl:template match="Document">
        <xsl:apply-templates/>
    </xsl:template>

    <xsl:template match="Sect">
        <xsl:apply-templates/>
    </xsl:template>

    <!-- ============================================================ -->
    <!-- SUPPRESSION TEMPLATES                                        -->
    <!-- ============================================================ -->

    <xsl:template match="x:xmpmeta | rdf:RDF" priority="10"/>

    <xsl:template match="bookmark-tree" priority="10"/>

    <xsl:template match="Artifact" priority="10">
        <xsl:if test="@* or normalize-space(.) != ''">
            <xsl:apply-templates/>
        </xsl:if>
    </xsl:template>

    <xsl:template match="processing-instruction('xpacket')" priority="10"/>

    <!-- ============================================================ -->
    <!-- PARAGRAPH TRANSFORMATION TEMPLATES                           -->
    <!-- ============================================================ -->

    <xsl:template match="P" priority="10">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:if test="$text != '' and not(ends-with($text, '(continued)'))">
            <p>
                <xsl:apply-templates select="@*"/>
                <xsl:apply-templates/>
            </p>
        </xsl:if>
    </xsl:template>

    <!-- ============================================================ -->
    <!-- LINK TRANSFORMATION TEMPLATES                                -->
    <!-- ============================================================ -->

    <xsl:template match="Reference">
        <a href="#">
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </a>
    </xsl:template>

</xsl:stylesheet>
