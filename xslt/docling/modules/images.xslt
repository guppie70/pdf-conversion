<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                exclude-result-prefixes="xs">

    <!-- Image transformation templates (default mode) - Docling HTML → Taxxor TDM format -->

    <!-- Transform img elements to use Taxxor DM image paths -->
    <xsl:template match="img" priority="10">
        <xsl:variable name="original-src" select="@src"/>

        <!-- Construct Taxxor-compatible path -->
        <xsl:variable name="taxxor-path" select="concat('/dataserviceassets/{projectid}/images/from-conversion/', $original-src)"/>

        <img src="{$taxxor-path}">
            <!-- Copy all attributes except src -->
            <xsl:apply-templates select="@*[not(name()='src')]"/>
            <!-- Copy any child nodes (though img is typically self-closing) -->
            <xsl:apply-templates select="node()"/>
        </img>
    </xsl:template>

    <!-- Copy img attributes (except src which is handled above) -->
    <xsl:template match="img/@*" priority="5">
        <xsl:copy/>
    </xsl:template>

</xsl:stylesheet>
