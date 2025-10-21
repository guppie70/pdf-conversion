<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                xmlns:lst="http://taxxor.com/xslt/list-functions"
                exclude-result-prefixes="xs lst">

    <!-- List transformation templates (default mode) -->

    <!-- List prefix stripping function -->

    <!-- Strip common list item prefixes (numbers, bullets, dashes, etc.) -->
    <xsl:function name="lst:strip-list-prefix" as="xs:string">
        <xsl:param name="text" as="xs:string"/>

        <xsl:variable name="normalized" select="normalize-space($text)"/>

        <!-- Pattern: numbers, letters, bullets, dashes, asterisks + whitespace -->
        <xsl:variable name="prefix-pattern"
                      select="'^(\d+\.|[a-zA-Z]\.|[ivxIVX]+\.|\d+\)|\([0-9]+\)|[a-zA-Z]\)|\([a-zA-Z]\)|[•◦▪▫∙⚫○\-–—ꟷ\*\+>])\s+'"/>

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
            <!-- L contains only nested L elements (no LI children): unwrap and process children -->
            <xsl:when test="L and not(LI)">
                <xsl:apply-templates/>
            </xsl:when>
            <!-- L with LI children: render as simple list (Pass 2 will extract headers) -->
            <xsl:otherwise>
                <xsl:choose>
                    <xsl:when test="@ListType='Ordered'">
                        <ol>
                            <xsl:apply-templates select="@* except @ListType"/>
                            <xsl:apply-templates select="LI"/>
                        </ol>
                    </xsl:when>
                    <xsl:otherwise>
                        <ul>
                            <xsl:apply-templates select="@* except @ListType"/>
                            <xsl:apply-templates select="LI"/>
                        </ul>
                    </xsl:otherwise>
                </xsl:choose>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <!-- LI template: check for forced headers and transform to header element with marker -->
    <xsl:template match="LI" priority="10">
        <xsl:choose>
            <!-- LI contains LBody with data-forceheader: transform to header element -->
            <xsl:when test="LBody/@data-forceheader">
                <li>
                    <xsl:apply-templates select="@*"/>
                    <!-- Process preceding content (if any) -->
                    <xsl:apply-templates select="LBody/preceding-sibling::node()"/>
                    <!-- Create header element with extraction marker -->
                    <xsl:variable name="header-level" select="string(LBody/@data-forceheader)"/>
                    <xsl:element name="{$header-level}">
                        <xsl:attribute name="data-extracted-header">true</xsl:attribute>
                        <xsl:attribute name="data-debug-level"><xsl:value-of select="$header-level"/></xsl:attribute>
                        <!-- Copy other attributes from LBody except data-forceheader -->
                        <xsl:apply-templates select="LBody/@* except LBody/@data-forceheader"/>
                        <!-- Process content with prefix stripping -->
                        <xsl:apply-templates select="LBody/node()" mode="strip-prefix"/>
                    </xsl:element>
                    <!-- Process following content (if any) -->
                    <xsl:apply-templates select="LBody/following-sibling::node()"/>
                </li>
            </xsl:when>
            <!-- Regular LI: process normally -->
            <xsl:otherwise>
                <li>
                    <xsl:apply-templates select="@*"/>
                    <xsl:apply-templates/>
                </li>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <!-- LBody: Output content directly, strip list prefixes from text -->
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

    <!-- Nested L elements within LBody: unwrap all wrapper L elements and forced headers -->
    <xsl:template match="L" mode="strip-prefix" priority="20">
        <xsl:choose>
            <!-- L contains only nested L elements (wrapper): unwrap and continue in strip-prefix mode -->
            <xsl:when test="L and not(LI)">
                <xsl:apply-templates mode="strip-prefix"/>
            </xsl:when>
            <!-- L has LI children but contains forced headers: unwrap completely (no list, just content) -->
            <xsl:when test="LI and .//LBody/@data-forceheader">
                <xsl:apply-templates select="LI" mode="strip-prefix"/>
            </xsl:when>
            <!-- L has LI children and no forced headers: this is a real nested list -->
            <xsl:otherwise>
                <xsl:apply-templates select="." mode="#default"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <!-- LI in strip-prefix mode: check for forced headers and extract directly (no li wrapper) -->
    <xsl:template match="LI" mode="strip-prefix" priority="20">
        <xsl:choose>
            <!-- LI contains forced header: extract header directly without li wrapper -->
            <xsl:when test="LBody/@data-forceheader">
                <xsl:variable name="header-level" select="string(LBody/@data-forceheader)"/>
                <xsl:element name="{$header-level}">
                    <xsl:attribute name="data-extracted-header">true</xsl:attribute>
                    <xsl:attribute name="data-debug-level"><xsl:value-of select="$header-level"/></xsl:attribute>
                    <xsl:apply-templates select="LBody/@* except LBody/@data-forceheader"/>
                    <xsl:apply-templates select="LBody/node()" mode="strip-prefix"/>
                </xsl:element>
            </xsl:when>
            <!-- Regular LI in strip-prefix: just output content (shouldn't happen in normal flow) -->
            <xsl:otherwise>
                <xsl:apply-templates mode="strip-prefix"/>
            </xsl:otherwise>
        </xsl:choose>
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
