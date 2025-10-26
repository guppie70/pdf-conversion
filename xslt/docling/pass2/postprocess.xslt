<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                exclude-result-prefixes="xs">

    <!-- Pass 2: Post-processing cleanup and asymmetric row detection -->

    <!-- Identity transform for pass2 (copy everything by default) -->
    <xsl:template match="node() | @*" mode="pass2">
        <xsl:copy>
            <xsl:apply-templates select="node() | @*" mode="pass2"/>
        </xsl:copy>
    </xsl:template>

    <!-- Detect asymmetric table rows: mark rows with fewer cells than table maximum -->
    <xsl:template match="table" mode="pass2" priority="10">
        <!-- Calculate max cell count across all rows -->
        <xsl:variable name="max-cells" as="xs:integer">
            <xsl:choose>
                <xsl:when test=".//tr">
                    <xsl:value-of select="max(.//tr/(count(td) + count(th)))"/>
                </xsl:when>
                <xsl:otherwise>0</xsl:otherwise>
            </xsl:choose>
        </xsl:variable>

        <xsl:copy>
            <xsl:apply-templates select="@*" mode="pass2"/>

            <!-- Process table children with max-cells context -->
            <xsl:apply-templates select="node()" mode="pass2-table">
                <xsl:with-param name="max-cells" select="$max-cells" tunnel="yes"/>
            </xsl:apply-templates>
        </xsl:copy>
    </xsl:template>

    <!-- Pass2-table mode: specialized table processing -->

    <xsl:template match="node() | @*" mode="pass2-table">
        <xsl:copy>
            <xsl:apply-templates select="node() | @*" mode="pass2-table"/>
        </xsl:copy>
    </xsl:template>

    <!-- Mark rows with fewer cells than maximum -->
    <xsl:template match="tr" mode="pass2-table" priority="10">
        <xsl:param name="max-cells" as="xs:integer" tunnel="yes"/>
        <xsl:variable name="current-cells" select="count(td) + count(th)"/>

        <xsl:copy>
            <xsl:apply-templates select="@*" mode="pass2-table"/>

            <!-- Add asymmetry markers if row is short -->
            <xsl:if test="$current-cells lt $max-cells">
                <xsl:attribute name="data-asymmetric">true</xsl:attribute>
                <xsl:attribute name="data-cell-count">
                    <xsl:value-of select="$current-cells"/>
                </xsl:attribute>
                <xsl:attribute name="data-expected-count">
                    <xsl:value-of select="$max-cells"/>
                </xsl:attribute>
            </xsl:if>

            <xsl:apply-templates select="node()" mode="pass2-table"/>
        </xsl:copy>
    </xsl:template>

</xsl:stylesheet>
