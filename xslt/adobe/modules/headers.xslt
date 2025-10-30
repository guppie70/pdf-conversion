<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                xmlns:hdr="http://taxxor.com/xslt/header-functions"
                exclude-result-prefixes="xs hdr">

    <!-- Header transformation templates (default mode) -->

    <xsl:template match="H1" priority="10">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="text-without-number" select="hdr:get-text-without-section-number($text)"/>
        <h1>
            <xsl:call-template name="add-numbering-attributes">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:value-of select="$text-without-number"/>
        </h1>
    </xsl:template>

    <xsl:template match="H2" priority="10">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="text-without-number" select="hdr:get-text-without-section-number($text)"/>
        <h2>
            <xsl:call-template name="add-numbering-attributes">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:value-of select="$text-without-number"/>
        </h2>
    </xsl:template>

    <xsl:template match="H3" priority="10">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="text-without-number" select="hdr:get-text-without-section-number($text)"/>
        <h3>
            <xsl:call-template name="add-numbering-attributes">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:value-of select="$text-without-number"/>
        </h3>
    </xsl:template>

    <xsl:template match="H4" priority="10">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="text-without-number" select="hdr:get-text-without-section-number($text)"/>
        <h4>
            <xsl:call-template name="add-numbering-attributes">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:value-of select="$text-without-number"/>
        </h4>
    </xsl:template>

    <xsl:template match="H5" priority="10">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="text-without-number" select="hdr:get-text-without-section-number($text)"/>
        <h5>
            <xsl:call-template name="add-numbering-attributes">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:value-of select="$text-without-number"/>
        </h5>
    </xsl:template>

    <xsl:template match="H6" priority="10">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="text-without-number" select="hdr:get-text-without-section-number($text)"/>
        <h6>
            <xsl:call-template name="add-numbering-attributes">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:value-of select="$text-without-number"/>
        </h6>
    </xsl:template>


    <!-- Deeply nested lists (6 levels): L>L>L>L>L>L>LI → h4 with auto-detected numberscheme -->
    <xsl:template match="L[L[L[L[L[L[LI]]]]]]" priority="35">
        <xsl:variable name="lbody" select="(.//LI)[1]/LBody"/>
        <xsl:variable name="text" select="normalize-space($lbody)"/>
        <xsl:variable name="text-without-prefix" select="hdr:get-text-without-prefix($text)"/>

        <xsl:if test="$text != '' and not(ends-with($text, '(continued)'))">
            <h4>
                <xsl:call-template name="add-numbering-attributes">
                    <xsl:with-param name="text" select="$text"/>
                </xsl:call-template>
                <xsl:value-of select="$text-without-prefix"/>
            </h4>
        </xsl:if>
    </xsl:template>

    <!-- Deeply nested lists (5 levels): L>L>L>L>L>LI → h3 (simple) or h3+h4s (nested sub-items) -->
    <xsl:template match="L[L[L[L[L[LI]]]]]" priority="30">
        <xsl:variable name="outerLBody" select="(.//LI)[1]/LBody"/>

        <xsl:choose>
            <xsl:when test="$outerLBody/L">
                <xsl:variable name="mainText">
                    <xsl:for-each select="$outerLBody/text()">
                        <xsl:value-of select="normalize-space(.)"/>
                        <xsl:if test="position() != last()">
                            <xsl:text> </xsl:text>
                        </xsl:if>
                    </xsl:for-each>
                </xsl:variable>

                <xsl:if test="normalize-space($mainText) != ''">
                    <xsl:variable name="mainTextClean" select="normalize-space($mainText)"/>
                    <xsl:variable name="mainTextWithoutPrefix" select="hdr:get-text-without-prefix($mainTextClean)"/>
                    <h3>
                        <xsl:call-template name="add-numbering-attributes">
                            <xsl:with-param name="text" select="$mainTextClean"/>
                        </xsl:call-template>
                        <xsl:value-of select="$mainTextWithoutPrefix"/>
                    </h3>
                </xsl:if>

                <xsl:for-each select="$outerLBody/L/LI/LBody">
                    <xsl:variable name="subText" select="normalize-space(.)"/>
                    <xsl:variable name="subTextWithoutPrefix" select="hdr:get-text-without-prefix($subText)"/>
                    <xsl:if test="$subText != ''">
                        <h4>
                            <xsl:call-template name="add-numbering-attributes">
                                <xsl:with-param name="text" select="$subText"/>
                            </xsl:call-template>
                            <xsl:value-of select="$subTextWithoutPrefix"/>
                        </h4>
                    </xsl:if>
                </xsl:for-each>
            </xsl:when>

            <xsl:otherwise>
                <xsl:variable name="text" select="normalize-space($outerLBody)"/>
                <xsl:variable name="text-without-prefix" select="hdr:get-text-without-prefix($text)"/>
                <xsl:if test="$text != '' and not(ends-with($text, '(continued)'))">
                    <xsl:variable name="detected-prefix" select="hdr:get-number-prefix($text)"/>
                    <h3>
                        <xsl:choose>
                            <xsl:when test="$detected-prefix != ''">
                                <xsl:call-template name="add-numbering-attributes">
                                    <xsl:with-param name="text" select="$text"/>
                                </xsl:call-template>
                            </xsl:when>
                            <xsl:otherwise>
                                <xsl:attribute name="data-numberscheme">(a),(b),(c)</xsl:attribute>
                                <xsl:attribute name="data-number"></xsl:attribute>
                            </xsl:otherwise>
                        </xsl:choose>
                        <xsl:value-of select="$text-without-prefix"/>
                    </h3>
                </xsl:if>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <!-- Numbering scheme detection functions -->

    <!-- Extract number prefix from text (e.g., "1.", "(a)", "ii.") -->
    <xsl:function name="hdr:get-number-prefix" as="xs:string">
        <xsl:param name="text" as="xs:string"/>
        <xsl:choose>
            <xsl:when test="matches($text, '^\([ivxlcdm]+\)\s')">
                <xsl:value-of select="replace($text, '^(\([ivxlcdm]+\))\s.*', '$1')"/>
            </xsl:when>
            <xsl:when test="matches($text, '^\([a-z]\)\s')">
                <xsl:value-of select="replace($text, '^(\([a-z]\))\s.*', '$1')"/>
            </xsl:when>
            <xsl:when test="matches($text, '^\([A-Z]\)\s')">
                <xsl:value-of select="replace($text, '^(\([A-Z]\))\s.*', '$1')"/>
            </xsl:when>
            <xsl:when test="matches($text, '^[a-z]\.\s')">
                <xsl:value-of select="replace($text, '^([a-z]\.)\s.*', '$1')"/>
            </xsl:when>
            <xsl:when test="matches($text, '^[A-Z]\.\s')">
                <xsl:value-of select="replace($text, '^([A-Z]\.)\s.*', '$1')"/>
            </xsl:when>
            <xsl:when test="matches($text, '^\d+\.\s')">
                <xsl:value-of select="replace($text, '^(\d+\.)\s.*', '$1')"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="''"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:function>

    <!-- Get numbering scheme pattern (e.g., "1.,2.,3.") -->
    <xsl:function name="hdr:get-numberscheme" as="xs:string">
        <xsl:param name="text" as="xs:string"/>
        <xsl:choose>
            <xsl:when test="matches($text, '^\([ivxlcdm]+\)\s')">
                <xsl:value-of select="'(i),(ii),(iii)'"/>
            </xsl:when>
            <xsl:when test="matches($text, '^\([a-z]\)\s')">
                <xsl:value-of select="'(a),(b),(c)'"/>
            </xsl:when>
            <xsl:when test="matches($text, '^\([A-Z]\)\s')">
                <xsl:value-of select="'(A),(B),(C)'"/>
            </xsl:when>
            <xsl:when test="matches($text, '^[a-z]\.\s')">
                <xsl:value-of select="'a.,b.,c.'"/>
            </xsl:when>
            <xsl:when test="matches($text, '^[A-Z]\.\s')">
                <xsl:value-of select="'A.,B.,C.'"/>
            </xsl:when>
            <xsl:when test="matches($text, '^\d+\.\s')">
                <xsl:value-of select="'1.,2.,3.'"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="''"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:function>

    <!-- Get text without number prefix -->
    <xsl:function name="hdr:get-text-without-prefix" as="xs:string">
        <xsl:param name="text" as="xs:string"/>
        <xsl:variable name="prefix" select="hdr:get-number-prefix($text)"/>
        <xsl:choose>
            <xsl:when test="$prefix != ''">
                <xsl:value-of select="normalize-space(substring-after($text, $prefix))"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="$text"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:function>

    <!-- Extract section number (e.g., "20.", "3.1", "3.1.1", "17") from text -->
    <xsl:function name="hdr:get-section-number" as="xs:string">
        <xsl:param name="text" as="xs:string"/>
        <xsl:choose>
            <!-- Match "17. Text" or "3.1. Text" (with period) -->
            <xsl:when test="matches($text, '^\s*(\d+(?:\.\d+)*\.)\s+')">
                <xsl:value-of select="replace($text, '^\s*(\d+(?:\.\d+)*\.)\s+.*', '$1')"/>
            </xsl:when>
            <!-- Match "17 Text" or "3 Text" (without period) -->
            <xsl:when test="matches($text, '^\s*(\d+)\s+')">
                <xsl:value-of select="replace($text, '^\s*(\d+)\s+.*', '$1')"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="''"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:function>

    <!-- Get text without section number -->
    <xsl:function name="hdr:get-text-without-section-number" as="xs:string">
        <xsl:param name="text" as="xs:string"/>
        <xsl:variable name="section-number" select="hdr:get-section-number($text)"/>
        <xsl:choose>
            <xsl:when test="$section-number != ''">
                <!-- Remove "17. " or "3.1. " or "17 " from start -->
                <xsl:value-of select="normalize-space(replace($text, '^\s*\d+(?:\.\d+)*\.?\s+', ''))"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="$text"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:function>

    <!-- Add data-numberscheme and data-number attributes -->
    <xsl:template name="add-numbering-attributes">
        <xsl:param name="text"/>
        <!-- First check for section numbers (e.g., "3.1", "3.1.1") -->
        <xsl:variable name="section-number" select="hdr:get-section-number($text)"/>
        <xsl:choose>
            <xsl:when test="$section-number != ''">
                <xsl:attribute name="data-number" select="$section-number"/>
            </xsl:when>
            <xsl:otherwise>
                <!-- Fall back to list-style number prefixes -->
                <xsl:variable name="scheme" select="hdr:get-numberscheme($text)"/>
                <xsl:variable name="number" select="hdr:get-number-prefix($text)"/>
                <xsl:if test="$scheme != ''">
                    <xsl:attribute name="data-numberscheme" select="$scheme"/>
                </xsl:if>
                <xsl:if test="$number != ''">
                    <xsl:attribute name="data-number" select="$number"/>
                </xsl:if>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <!-- Numbered section headings: L with single LI starting with "1." → h2 -->
    <xsl:template match="L[count(LI) = 1 and matches(normalize-space(LI/LBody), '^\d+\.\s+')]" priority="20">
        <xsl:variable name="text" select="normalize-space(LI/LBody)"/>
        <xsl:if test="not(ends-with($text, '(continued)'))">
            <xsl:variable name="heading" select="hdr:get-text-without-prefix($text)"/>
            <h2>
                <xsl:call-template name="add-numbering-attributes">
                    <xsl:with-param name="text" select="$text"/>
                </xsl:call-template>
                <xsl:value-of select="$heading"/>
            </h2>
        </xsl:if>
    </xsl:template>

    <!-- Paragraph-to-header conversion: <P> with numbering prefixes → <h4> -->

    <!-- P with data-forceheader attribute → forced header level with (a),(b),(c) numbering scheme (priority=20) -->
    <xsl:template match="P[@data-forceheader and normalize-space(.) != '']"
                  priority="20">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="header-level" select="@data-forceheader"/>

        <xsl:element name="{$header-level}">
            <xsl:attribute name="data-numberscheme">(a),(b),(c)</xsl:attribute>
            <xsl:attribute name="data-number"></xsl:attribute>
            <xsl:value-of select="$text"/>
        </xsl:element>
    </xsl:template>

    <!-- P with numbering prefixes → h4 with detected numberscheme (priority=15 to intercept default P template) -->
    <xsl:template match="P[normalize-space(.) != '' and
                           not(ends-with(normalize-space(.), '(continued)')) and
                           hdr:get-number-prefix(normalize-space(.)) != '']"
                  priority="15">
        <xsl:variable name="text" select="normalize-space(.)"/>
        <xsl:variable name="text-without-prefix" select="hdr:get-text-without-prefix($text)"/>
        <h4>
            <xsl:call-template name="add-numbering-attributes">
                <xsl:with-param name="text" select="$text"/>
            </xsl:call-template>
            <xsl:value-of select="$text-without-prefix"/>
        </h4>
    </xsl:template>


</xsl:stylesheet>
