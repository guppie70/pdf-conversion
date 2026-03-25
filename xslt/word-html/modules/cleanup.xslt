<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

    <!-- Strip all <span> elements, keeping their content -->
    <xsl:template match="span" priority="5">
        <xsl:apply-templates/>
    </xsl:template>

    <!-- Remove empty paragraphs (content is empty or only whitespace/non-breaking spaces) -->
    <xsl:template match="p[not(normalize-space(translate(., '&#xA0;', ' ')))]" priority="6"/>

    <!-- Remove empty divs (only whitespace/non-breaking spaces, no meaningful content) -->
    <xsl:template match="div[not(normalize-space(translate(., '&#xA0;', ' ')))]" priority="6"/>

    <!-- Clean <p> elements: strip class and style attributes -->
    <xsl:template match="p" priority="5">
        <p><xsl:apply-templates/></p>
    </xsl:template>

    <!-- Strip all @style attributes -->
    <xsl:template match="@style" priority="5"/>

    <!-- Drop whitespace-only text nodes (including &#160; non-breaking spaces from Word), collapse whitespace in others -->
    <xsl:template match="text()" priority="3">
        <xsl:variable name="with-regular-spaces" select="translate(., '&#xA0;', ' ')"/>
        <xsl:if test="normalize-space($with-regular-spaces) != ''">
            <xsl:value-of select="replace($with-regular-spaces, '\s+', ' ')"/>
        </xsl:if>
    </xsl:template>

</xsl:stylesheet>
