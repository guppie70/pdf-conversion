<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                exclude-result-prefixes="xs">

    <!-- Header transformation templates (default mode) - data-forceheader support -->

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
