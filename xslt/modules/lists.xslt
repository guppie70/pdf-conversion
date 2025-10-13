<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns="http://www.w3.org/1999/xhtml"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                xmlns:lst="http://taxxor.com/xslt/list-functions"
                exclude-result-prefixes="xs lst">

    <!-- ============================================================ -->
    <!-- LIST TRANSFORMATION TEMPLATES                                -->
    <!-- ============================================================ -->

    <!-- ============================================================ -->
    <!-- LIST PREFIX STRIPPING FUNCTION                               -->
    <!-- ============================================================ -->

    <!-- Strip common list item prefixes (numbers, bullets, dashes, etc.) -->
    <xsl:function name="lst:strip-list-prefix" as="xs:string">
        <xsl:param name="text" as="xs:string"/>

        <xsl:variable name="normalized" select="normalize-space($text)"/>

        <!-- Pattern matches:
             - Numbers with period: "1.", "12.", "123."
             - Numbers with parenthesis: "1)", "(1)"
             - Letters with period: "a.", "A.", "i.", "I.", "iv.", "IX."
             - Letters with parenthesis: "a)", "(a)", "A)", "(A)"
             - Bullets: "•", "◦", "▪", "▫", "∙", "⚫", "○"
             - Dashes: "-", "–", "—" (hyphen, en-dash, em-dash)
             - Asterisks: "*"
             - Plus signs: "+"
             - Greater than: ">"
             Followed by whitespace (at least one space required after prefix) -->
        <xsl:variable name="prefix-pattern"
                      select="'^(\d+\.|[a-zA-Z]\.|[ivxIVX]+\.|\d+\)|\([0-9]+\)|[a-zA-Z]\)|\([a-zA-Z]\)|[•◦▪▫∙⚫○\-–—\*\+>])\s+'"/>

        <!-- Only strip prefix if pattern matches (avoids zero-length match error) -->
        <xsl:choose>
            <xsl:when test="matches($normalized, $prefix-pattern)">
                <xsl:value-of select="normalize-space(replace($normalized, $prefix-pattern, ''))"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="$normalized"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:function>

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

    <!-- LBody: Output content directly without wrapping element, strip list prefixes from text -->
    <xsl:template match="LBody" priority="10">
        <xsl:apply-templates select="@*"/>
        <xsl:apply-templates mode="strip-prefix"/>
    </xsl:template>

    <!-- Text node processing within LBody - strip prefixes -->
    <xsl:template match="text()" mode="strip-prefix">
        <xsl:variable name="stripped" select="lst:strip-list-prefix(.)"/>
        <xsl:if test="$stripped != ''">
            <xsl:value-of select="$stripped"/>
        </xsl:if>
    </xsl:template>

    <!-- Pass through other nodes in strip-prefix mode -->
    <xsl:template match="*" mode="strip-prefix">
        <xsl:copy>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates mode="strip-prefix"/>
        </xsl:copy>
    </xsl:template>

    <xsl:template match="@*" mode="strip-prefix">
        <xsl:copy/>
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
