<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

    <!-- Footnote reference links: simplify <a> containing MsoFootnoteReference spans
         to just <a href="..."><sup>text</sup></a> -->

    <xsl:template match="a[.//span[contains(@class, 'MsoFootnoteReference')]]" priority="10">
        <a href="{@href}">
            <sup><xsl:value-of select="normalize-space(.)"/></sup>
        </a>
    </xsl:template>

</xsl:stylesheet>
