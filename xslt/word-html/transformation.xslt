<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

    <xsl:output method="xml" encoding="UTF-8" indent="no" omit-xml-declaration="yes"/>

    <!-- Include image path rewriting (xsl:include matches existing codebase convention) -->
    <xsl:include href="modules/images.xslt"/>

    <!-- Include footnote reference simplification -->
    <xsl:include href="modules/footnotes.xslt"/>

    <!-- Include span/paragraph cleanup -->
    <xsl:include href="modules/cleanup.xslt"/>

    <!-- Identity transform: copy everything unchanged -->
    <xsl:template match="@*|node()">
        <xsl:copy>
            <xsl:apply-templates select="@*|node()"/>
        </xsl:copy>
    </xsl:template>

</xsl:stylesheet>
