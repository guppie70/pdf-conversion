<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns="http://www.w3.org/1999/xhtml"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                exclude-result-prefixes="xs">

    <!-- ============================================================ -->
    <!-- LIST TRANSFORMATION TEMPLATES                                -->
    <!-- ============================================================ -->

    <!-- Deeply nested lists (6 levels) with single LI containing headings -->
    <!-- Matches: L > L > L > L > L > L > LI -->
    <!-- Outputs: h4 with auto-detected data-numberscheme attribute -->
    <xsl:template match="L[L[L[L[L[L[LI]]]]]]" priority="35">
        <xsl:variable name="lbody" select="(.//LI)[1]/LBody"/>
        <xsl:variable name="text" select="normalize-space($lbody)"/>

        <!-- Suppress if empty or ends with "(continued)" -->
        <xsl:if test="$text != '' and not(ends-with($text, '(continued)'))">
            <h4>
                <!-- Detect and add data-numberscheme attribute based on text pattern -->
                <xsl:call-template name="add-numberscheme-attribute">
                    <xsl:with-param name="text" select="$text"/>
                </xsl:call-template>
                <xsl:value-of select="$text"/>
            </h4>
        </xsl:if>
    </xsl:template>

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
                <!-- Suppress if empty or ends with "(continued)" -->
                <xsl:if test="$text != '' and not(ends-with($text, '(continued)'))">
                    <h3>
                        <xsl:value-of select="$text"/>
                    </h3>
                </xsl:if>
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
        <!-- Suppress if ends with "(continued)" -->
        <xsl:if test="not(ends-with($text, '(continued)'))">
            <xsl:variable name="number" select="replace($text, '^(\d+\.)\s+.*', '$1')"/>
            <xsl:variable name="heading" select="replace($text, '^\d+\.\s+(.*)', '$1')"/>
            <h2 data-number="{$number}" data-numberscheme="1.,2.,3.">
                <xsl:value-of select="$heading"/>
            </h2>
        </xsl:if>
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

</xsl:stylesheet>
