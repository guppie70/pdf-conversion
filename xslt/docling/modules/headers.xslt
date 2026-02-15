<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                xmlns:hdr="http://taxxor.com/xslt/header-functions"
                exclude-result-prefixes="xs hdr">

    <!-- Header transformation templates (default mode) - prefix and postfix section number stripping -->

    <!-- Extract section number from START of text (e.g., "14. Rounding off" → "14.") -->
    <xsl:function name="hdr:get-section-number-prefix" as="xs:string">
        <xsl:param name="text" as="xs:string"/>
        <xsl:choose>
            <!-- Match prefix patterns: "14. Text" or "2.1 Text" or "3.1.2 Text" -->
            <!-- Exclude 4-digit years (20xx range) - e.g., 2000-2099 -->
            <xsl:when test="matches($text, '^\s*\d+(?:\.\d+)*\.?\s+') and not(matches($text, '^\s*20\d{2}\s+'))">
                <xsl:value-of select="replace($text, '^\s*(\d+(?:\.\d+)*\.?)\s+.*$', '$1')"/>
            </xsl:when>
            <!-- Match parenthetical prefixes: "(1) Text", "(i) Text", "(a) Text" -->
            <xsl:when test="matches($text, '^\s*\([0-9]+\)\s+')">
                <xsl:value-of select="replace($text, '^\s*(\([0-9]+\))\s+.*$', '$1')"/>
            </xsl:when>
            <xsl:when test="matches($text, '^\s*\([ivxIVX]+\)\s+')">
                <xsl:value-of select="replace($text, '^\s*(\([ivxIVX]+\))\s+.*$', '$1')"/>
            </xsl:when>
            <xsl:when test="matches($text, '^\s*\([a-zA-Z]\)\s+')">
                <xsl:value-of select="replace($text, '^\s*(\([a-zA-Z]\))\s+.*$', '$1')"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="''"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:function>

    <!-- Get text without prefix section number -->
    <xsl:function name="hdr:get-text-without-section-number-prefix" as="xs:string">
        <xsl:param name="text" as="xs:string"/>
        <xsl:variable name="section-number" select="hdr:get-section-number-prefix($text)"/>
        <xsl:choose>
            <xsl:when test="$section-number != ''">
                <!-- Remove the matched section number from the start -->
                <xsl:choose>
                    <xsl:when test="matches($text, '^\s*\d+(?:\.\d+)*\.?\s+') and not(matches($text, '^\s*20\d{2}\s+'))">
                        <xsl:value-of select="normalize-space(replace($text, '^\s*\d+(?:\.\d+)*\.?\s+', ''))"/>
                    </xsl:when>
                    <xsl:when test="matches($text, '^\s*\([0-9]+\)\s+')">
                        <xsl:value-of select="normalize-space(replace($text, '^\s*\([0-9]+\)\s+', ''))"/>
                    </xsl:when>
                    <xsl:when test="matches($text, '^\s*\([ivxIVX]+\)\s+')">
                        <xsl:value-of select="normalize-space(replace($text, '^\s*\([ivxIVX]+\)\s+', ''))"/>
                    </xsl:when>
                    <xsl:when test="matches($text, '^\s*\([a-zA-Z]\)\s+')">
                        <xsl:value-of select="normalize-space(replace($text, '^\s*\([a-zA-Z]\)\s+', ''))"/>
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

    <!-- Add data-number attribute with section number (check prefix first, then postfix) -->
    <xsl:template name="add-section-number-attribute">
        <xsl:param name="text"/>
        <xsl:variable name="prefix-number" select="hdr:get-section-number-prefix($text)"/>
        <xsl:variable name="postfix-number" select="hdr:get-section-number-postfix($text)"/>
        <xsl:variable name="section-number" select="if ($prefix-number != '') then $prefix-number else $postfix-number"/>
        <xsl:if test="$section-number != ''">
            <xsl:attribute name="data-number" select="$section-number"/>
        </xsl:if>
    </xsl:template>

    <!-- Strip empty headers (h1-h6 with no text content) -->
    <xsl:template match="h1[normalize-space(.) = ''] | h2[normalize-space(.) = ''] | h3[normalize-space(.) = ''] | h4[normalize-space(.) = ''] | h5[normalize-space(.) = ''] | h6[normalize-space(.) = '']" priority="25"/>

    <!-- H1 with "(continued)" suppression - higher priority -->
    <xsl:template match="h1[contains(normalize-space(.), '(continued)')]" priority="20"/>

    <!-- H1 with prefix/postfix section number stripping -->
    <xsl:template match="h1" priority="15">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="prefix-number" select="hdr:get-section-number-prefix($text)"/>
        <xsl:variable name="text-without-number" select="if ($prefix-number != '')
            then hdr:get-text-without-section-number-prefix($text)
            else hdr:get-text-without-section-number-postfix($text)"/>
        <h1>
            <xsl:call-template name="add-section-number-attribute">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:apply-templates select="@*"/>
            <xsl:value-of select="$text-without-number"/>
        </h1>
    </xsl:template>

    <!-- H2 with "(continued)" suppression - higher priority -->
    <xsl:template match="h2[contains(normalize-space(.), '(continued)')]" priority="20"/>

    <!-- H2 with prefix/postfix section number stripping -->
    <xsl:template match="h2" priority="15">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="prefix-number" select="hdr:get-section-number-prefix($text)"/>
        <xsl:variable name="text-without-number" select="if ($prefix-number != '')
            then hdr:get-text-without-section-number-prefix($text)
            else hdr:get-text-without-section-number-postfix($text)"/>
        <h2>
            <xsl:call-template name="add-section-number-attribute">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:apply-templates select="@*"/>
            <xsl:value-of select="$text-without-number"/>
        </h2>
    </xsl:template>

    <!-- H3 with "(continued)" suppression - higher priority -->
    <xsl:template match="h3[contains(normalize-space(.), '(continued)')]" priority="20"/>

    <!-- H3 with prefix/postfix section number stripping -->
    <xsl:template match="h3" priority="15">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="prefix-number" select="hdr:get-section-number-prefix($text)"/>
        <xsl:variable name="text-without-number" select="if ($prefix-number != '')
            then hdr:get-text-without-section-number-prefix($text)
            else hdr:get-text-without-section-number-postfix($text)"/>
        <h3>
            <xsl:call-template name="add-section-number-attribute">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:apply-templates select="@*"/>
            <xsl:value-of select="$text-without-number"/>
        </h3>
    </xsl:template>

    <!-- H4 with "(continued)" suppression - higher priority -->
    <xsl:template match="h4[contains(normalize-space(.), '(continued)')]" priority="20"/>

    <!-- H4 with prefix/postfix section number stripping -->
    <xsl:template match="h4" priority="15">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="prefix-number" select="hdr:get-section-number-prefix($text)"/>
        <xsl:variable name="text-without-number" select="if ($prefix-number != '')
            then hdr:get-text-without-section-number-prefix($text)
            else hdr:get-text-without-section-number-postfix($text)"/>
        <h4>
            <xsl:call-template name="add-section-number-attribute">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:apply-templates select="@*"/>
            <xsl:value-of select="$text-without-number"/>
        </h4>
    </xsl:template>

    <!-- H5 with "(continued)" suppression - higher priority -->
    <xsl:template match="h5[contains(normalize-space(.), '(continued)')]" priority="20"/>

    <!-- H5 with prefix/postfix section number stripping -->
    <xsl:template match="h5" priority="15">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="prefix-number" select="hdr:get-section-number-prefix($text)"/>
        <xsl:variable name="text-without-number" select="if ($prefix-number != '')
            then hdr:get-text-without-section-number-prefix($text)
            else hdr:get-text-without-section-number-postfix($text)"/>
        <h5>
            <xsl:call-template name="add-section-number-attribute">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:apply-templates select="@*"/>
            <xsl:value-of select="$text-without-number"/>
        </h5>
    </xsl:template>

    <!-- H6 with "(continued)" suppression - higher priority -->
    <xsl:template match="h6[contains(normalize-space(.), '(continued)')]" priority="20"/>

    <!-- H6 with prefix/postfix section number stripping -->
    <xsl:template match="h6" priority="15">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="prefix-number" select="hdr:get-section-number-prefix($text)"/>
        <xsl:variable name="text-without-number" select="if ($prefix-number != '')
            then hdr:get-text-without-section-number-prefix($text)
            else hdr:get-text-without-section-number-postfix($text)"/>
        <h6>
            <xsl:call-template name="add-section-number-attribute">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:apply-templates select="@*"/>
            <xsl:value-of select="$text-without-number"/>
        </h6>
    </xsl:template>

    <!-- Paragraph with prefix number: extract to data-number attribute -->
    <xsl:template match="p[not(@data-forceheader) and normalize-space(.) != '']" priority="15">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="prefix-number" select="hdr:get-section-number-prefix($text)"/>
        <xsl:choose>
            <xsl:when test="$prefix-number != ''">
                <!-- Has prefix number: extract and strip it -->
                <xsl:variable name="text-without-number" select="hdr:get-text-without-section-number-prefix($text)"/>
                <p>
                    <xsl:attribute name="data-number" select="$prefix-number"/>
                    <xsl:apply-templates select="@*"/>
                    <xsl:value-of select="$text-without-number"/>
                </p>
            </xsl:when>
            <xsl:otherwise>
                <!-- No prefix number: use standard copy -->
                <p>
                    <xsl:apply-templates select="@*"/>
                    <xsl:apply-templates/>
                </p>
            </xsl:otherwise>
        </xsl:choose>
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
