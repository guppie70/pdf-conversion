<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                xmlns:hdr="http://taxxor.com/xslt/header-functions"
                exclude-result-prefixes="xs hdr">

    <!-- Header transformation templates (default mode) - postfix section number stripping -->

    <!-- Extract section number from END of text (e.g., "Cost analysis 2.2" → "2.2") -->
    <xsl:function name="hdr:get-section-number-postfix" as="xs:string">
        <xsl:param name="text" as="xs:string"/>
        <xsl:choose>
            <!-- Match postfix patterns: "Text 1" or "Text 2.1" or "Text 3.1.2" -->
            <!-- Exclude 4-digit numbers (likely years like 2025) -->
            <xsl:when test="matches($text, '\s+\d+(?:\.\d+)+\s*$')">
                <!-- Multi-level numbers (e.g., "2.2", "3.1.2") - always section numbers -->
                <xsl:value-of select="replace($text, '^.*\s+(\d+(?:\.\d+)+)\s*$', '$1')"/>
            </xsl:when>
            <xsl:when test="matches($text, '\s+\d{1,3}\s*$')">
                <!-- Single-level 1-3 digit numbers (e.g., "1", "2", "99") - section numbers -->
                <xsl:value-of select="replace($text, '^.*\s+(\d{1,3})\s*$', '$1')"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="''"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:function>

    <!-- Get text without postfix section number -->
    <xsl:function name="hdr:get-text-without-section-number-postfix" as="xs:string">
        <xsl:param name="text" as="xs:string"/>
        <xsl:variable name="section-number" select="hdr:get-section-number-postfix($text)"/>
        <xsl:choose>
            <xsl:when test="$section-number != ''">
                <!-- Remove the matched section number from the end -->
                <xsl:choose>
                    <xsl:when test="matches($text, '\s+\d+(?:\.\d+)+\s*$')">
                        <xsl:value-of select="normalize-space(replace($text, '\s+\d+(?:\.\d+)+\s*$', ''))"/>
                    </xsl:when>
                    <xsl:when test="matches($text, '\s+\d{1,3}\s*$')">
                        <xsl:value-of select="normalize-space(replace($text, '\s+\d{1,3}\s*$', ''))"/>
                    </xsl:when>
                    <xsl:otherwise>
                        <xsl:value-of select="$text"/>
                    </xsl:otherwise>
                </xsl:choose>
            </xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="$text"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:function>

    <!-- Add data-number attribute with section number -->
    <xsl:template name="add-section-number-attribute">
        <xsl:param name="text"/>
        <xsl:variable name="section-number" select="hdr:get-section-number-postfix($text)"/>
        <xsl:if test="$section-number != ''">
            <xsl:attribute name="data-number" select="$section-number"/>
        </xsl:if>
    </xsl:template>

    <!-- H1 with postfix section number stripping -->
    <xsl:template match="h1" priority="15">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="text-without-number" select="hdr:get-text-without-section-number-postfix($text)"/>
        <h1>
            <xsl:call-template name="add-section-number-attribute">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:apply-templates select="@*"/>
            <xsl:value-of select="$text-without-number"/>
        </h1>
    </xsl:template>

    <!-- H2 with postfix section number stripping -->
    <xsl:template match="h2" priority="15">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="text-without-number" select="hdr:get-text-without-section-number-postfix($text)"/>
        <h2>
            <xsl:call-template name="add-section-number-attribute">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:apply-templates select="@*"/>
            <xsl:value-of select="$text-without-number"/>
        </h2>
    </xsl:template>

    <!-- H3 with postfix section number stripping -->
    <xsl:template match="h3" priority="15">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="text-without-number" select="hdr:get-text-without-section-number-postfix($text)"/>
        <h3>
            <xsl:call-template name="add-section-number-attribute">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:apply-templates select="@*"/>
            <xsl:value-of select="$text-without-number"/>
        </h3>
    </xsl:template>

    <!-- H4 with postfix section number stripping -->
    <xsl:template match="h4" priority="15">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="text-without-number" select="hdr:get-text-without-section-number-postfix($text)"/>
        <h4>
            <xsl:call-template name="add-section-number-attribute">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:apply-templates select="@*"/>
            <xsl:value-of select="$text-without-number"/>
        </h4>
    </xsl:template>

    <!-- H5 with postfix section number stripping -->
    <xsl:template match="h5" priority="15">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="text-without-number" select="hdr:get-text-without-section-number-postfix($text)"/>
        <h5>
            <xsl:call-template name="add-section-number-attribute">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:apply-templates select="@*"/>
            <xsl:value-of select="$text-without-number"/>
        </h5>
    </xsl:template>

    <!-- H6 with postfix section number stripping -->
    <xsl:template match="h6" priority="15">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="text-without-number" select="hdr:get-text-without-section-number-postfix($text)"/>
        <h6>
            <xsl:call-template name="add-section-number-attribute">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:apply-templates select="@*"/>
            <xsl:value-of select="$text-without-number"/>
        </h6>
    </xsl:template>

    <!-- Paragraph-to-header conversion: <p data-forceheader="h2"> → <h2> with numbering attributes -->
    <xsl:template match="p[@data-forceheader and normalize-space(.) != '']" priority="20">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="header-level" select="@data-forceheader"/>

        <xsl:element name="{$header-level}">
            <xsl:attribute name="data-numberscheme">(a),(b),(c)</xsl:attribute>
            <xsl:attribute name="data-number"></xsl:attribute>
            <!-- Copy other attributes except data-forceheader -->
            <xsl:apply-templates select="@*[not(local-name() = 'data-forceheader')]"/>
            <xsl:value-of select="$text"/>
        </xsl:element>
    </xsl:template>

</xsl:stylesheet>
