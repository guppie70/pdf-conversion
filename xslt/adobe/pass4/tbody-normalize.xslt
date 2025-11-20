<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                exclude-result-prefixes="xs">

    <!-- Tbody normalization (Pass 4, mode="pass4") - Convert all <th> in <tbody> to <td> -->

    <!-- Identity transform for pass4 (copy everything by default) -->
    <xsl:template match="node() | @*" mode="pass4">
        <xsl:copy>
            <xsl:apply-templates select="node() | @*" mode="pass4"/>
        </xsl:copy>
    </xsl:template>

    <!-- Convert <th> elements inside <tbody> to <td> -->
    <xsl:template match="tbody//th" mode="pass4" priority="10">
        <td>
            <xsl:apply-templates select="@* | node()" mode="pass4"/>
        </td>
    </xsl:template>

</xsl:stylesheet>
