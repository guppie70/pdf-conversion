<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

    <!-- Strip all <span> elements, keeping their content -->
    <xsl:template match="span" priority="5">
        <xsl:apply-templates/>
    </xsl:template>

    <!-- Clean <p> elements: strip class and style attributes -->
    <xsl:template match="p" priority="5">
        <p><xsl:apply-templates/></p>
    </xsl:template>

    <!-- Collapse whitespace sequences (including newlines from Word source) into single spaces -->
    <xsl:template match="text()" priority="3">
        <xsl:value-of select="replace(., '\s+', ' ')"/>
    </xsl:template>

</xsl:stylesheet>
