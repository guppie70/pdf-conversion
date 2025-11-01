<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                exclude-result-prefixes="xs">

    <!-- Content stripping processing (Pass 4, mode="pass4") - Remove elements marked with data-strip="true" until data-strip="false" -->

    <!-- Identity transform for pass4 (copy everything by default) -->
    <xsl:template match="node() | @*" mode="pass4">
        <xsl:copy>
            <xsl:apply-templates select="node() | @*" mode="pass4"/>
        </xsl:copy>
    </xsl:template>

    <!-- Main template: process children with stripping logic -->
    <xsl:template match="*[p or h1 or h2 or h3 or h4 or h5 or h6 or img or table]" mode="pass4" priority="5">
        <xsl:copy>
            <xsl:apply-templates select="@*" mode="pass4"/>
            <xsl:call-template name="process-with-stripping">
                <xsl:with-param name="nodes" select="node()"/>
            </xsl:call-template>
        </xsl:copy>
    </xsl:template>

    <!-- Named template: process nodes with stripping mode tracking -->
    <xsl:template name="process-with-stripping">
        <xsl:param name="nodes" as="node()*"/>
        <xsl:param name="stripping" as="xs:boolean" select="false()"/>
        <xsl:param name="position" as="xs:integer" select="1"/>

        <xsl:if test="$position le count($nodes)">
            <xsl:variable name="current" select="$nodes[$position]"/>

            <xsl:choose>
                <!-- Element with data-strip="true" - start stripping, exclude this element -->
                <xsl:when test="$current[self::p or self::h1 or self::h2 or self::h3 or self::h4 or self::h5 or self::h6 or self::img or self::table][@data-strip='true']">
                    <xsl:call-template name="process-with-stripping">
                        <xsl:with-param name="nodes" select="$nodes"/>
                        <xsl:with-param name="stripping" select="true()"/>
                        <xsl:with-param name="position" select="$position + 1"/>
                    </xsl:call-template>
                </xsl:when>

                <!-- Element with data-strip="false" - stop stripping, include this element (without data-strip attr) -->
                <xsl:when test="$current[self::p or self::h1 or self::h2 or self::h3 or self::h4 or self::h5 or self::h6 or self::img or self::table][@data-strip='false']">
                    <xsl:apply-templates select="$current" mode="pass4-strip-attr"/>
                    <xsl:call-template name="process-with-stripping">
                        <xsl:with-param name="nodes" select="$nodes"/>
                        <xsl:with-param name="stripping" select="false()"/>
                        <xsl:with-param name="position" select="$position + 1"/>
                    </xsl:call-template>
                </xsl:when>

                <!-- Currently stripping - exclude this node -->
                <xsl:when test="$stripping">
                    <xsl:call-template name="process-with-stripping">
                        <xsl:with-param name="nodes" select="$nodes"/>
                        <xsl:with-param name="stripping" select="true()"/>
                        <xsl:with-param name="position" select="$position + 1"/>
                    </xsl:call-template>
                </xsl:when>

                <!-- Not stripping - include this node -->
                <xsl:otherwise>
                    <xsl:apply-templates select="$current" mode="pass4"/>
                    <xsl:call-template name="process-with-stripping">
                        <xsl:with-param name="nodes" select="$nodes"/>
                        <xsl:with-param name="stripping" select="false()"/>
                        <xsl:with-param name="position" select="$position + 1"/>
                    </xsl:call-template>
                </xsl:otherwise>
            </xsl:choose>
        </xsl:if>
    </xsl:template>

    <!-- Remove data-strip attributes from output -->
    <xsl:template match="@data-strip" mode="pass4" priority="10"/>

    <!-- Mode pass4-strip-attr: Like pass4 but explicitly removes data-strip attribute -->
    <xsl:template match="node()" mode="pass4-strip-attr">
        <xsl:copy>
            <xsl:apply-templates select="@*[not(local-name() = 'data-strip')]" mode="pass4"/>
            <xsl:apply-templates select="node()" mode="pass4"/>
        </xsl:copy>
    </xsl:template>

    <xsl:template match="@*" mode="pass4-strip-attr">
        <xsl:copy/>
    </xsl:template>

</xsl:stylesheet>
