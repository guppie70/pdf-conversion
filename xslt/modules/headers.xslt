<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns="http://www.w3.org/1999/xhtml"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                exclude-result-prefixes="xs">

    <!-- ============================================================ -->
    <!-- HEADER TRANSFORMATION TEMPLATES                              -->
    <!-- ============================================================ -->

    <xsl:template match="H1" priority="10">
        <h1>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </h1>
    </xsl:template>

    <xsl:template match="H2" priority="10">
        <h2>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </h2>
    </xsl:template>

    <xsl:template match="H3" priority="10">
        <h3>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </h3>
    </xsl:template>

</xsl:stylesheet>
