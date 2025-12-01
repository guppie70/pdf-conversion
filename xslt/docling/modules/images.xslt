<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                exclude-result-prefixes="xs">

    <!-- Image transformation templates (default mode) - Docling HTML → Taxxor TDM format -->

    <!-- Transform img elements to use Taxxor DM image paths -->
    <xsl:template match="img" priority="20">
        <xsl:variable name="original-src" select="@src"/>

        <!-- Construct Taxxor-compatible path -->
        <xsl:variable name="taxxor-path" select="concat('/dataserviceassets/{projectid}/images/from-conversion/', $original-src)"/>

        <img src="{$taxxor-path}">
            <!-- Preserve alt attribute from source, or use empty string for WCAG compliance -->
            <xsl:attribute name="alt">
                <xsl:value-of select="if (@alt) then @alt else ''"/>
            </xsl:attribute>
            <!-- Copy all other attributes except src and alt -->
            <xsl:apply-templates select="@*[not(name()='src' or name()='alt')]"/>
        </img>
    </xsl:template>

    <!-- Transform figure with figcaption into img with caption as alt and data-rendercaption -->
    <xsl:template match="figure[figcaption and img]" priority="25">
        <xsl:variable name="caption-text" select="normalize-space(figcaption//div[@class='caption'])"/>
        <xsl:variable name="original-src" select="img/@src"/>
        <xsl:variable name="taxxor-path" select="concat('/dataserviceassets/{projectid}/images/from-conversion/', $original-src)"/>

        <img src="{$taxxor-path}" alt="{$caption-text}" data-rendercaption="true">
            <!-- Copy other attributes from original img except src and alt -->
            <xsl:apply-templates select="img/@*[not(name()='src' or name()='alt')]"/>
        </img>
    </xsl:template>

    <!-- Fallback for figure without figcaption (just unwrap) -->
    <xsl:template match="figure[not(figcaption)]" priority="15">
        <xsl:apply-templates/>
    </xsl:template>

</xsl:stylesheet>
