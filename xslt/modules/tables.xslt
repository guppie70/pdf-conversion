<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns="http://www.w3.org/1999/xhtml"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                exclude-result-prefixes="xs">

    <!-- ============================================================ -->
    <!-- TABLE TRANSFORMATION TEMPLATES                               -->
    <!-- ============================================================ -->
    <!-- Transform Adobe PDF tables to Taxxor TDM table structure     -->
    <!-- with required wrapper divs and metadata attributes           -->
    <!-- ============================================================ -->

    <xsl:template match="Table" priority="10">
        <xsl:variable name="tableId" select="generate-id()"/>
        <div id="tablewrapper_{$tableId}"
             class="table-wrapper structured-data-table"
             data-instanceid="{generate-id()}-wrapper">
            <div class="tablegraph-header-wrapper">
                <div class="table-title">tabletitle</div>
                <div class="table-scale">scale</div>
            </div>
            <table id="table_{$tableId}"
                   class="tabletype-numbers"
                   data-instanceid="{generate-id()}-table">
                <xsl:choose>
                    <!-- Header rows must contain ONLY TH elements (no TD elements) -->
                    <!-- Mixed rows (containing both TH and TD) are treated as body rows -->
                    <xsl:when test="TR[1][TH and not(TD)]">
                        <thead>
                            <!-- Header rows: only TR elements containing TH and no TD -->
                            <xsl:apply-templates select="TR[TH and not(TD)]" mode="header"/>
                        </thead>
                        <tbody>
                            <!-- Body rows: only TR elements containing at least one TD -->
                            <!-- This ensures no row appears in both thead and tbody -->
                            <xsl:apply-templates select="TR[TD]" mode="body"/>
                        </tbody>
                    </xsl:when>
                    <xsl:otherwise>
                        <tbody>
                            <xsl:apply-templates select="TR" mode="body"/>
                        </tbody>
                    </xsl:otherwise>
                </xsl:choose>
            </table>
        </div>
    </xsl:template>

    <!-- Table row templates in header mode -->
    <xsl:template match="TR" mode="header" priority="10">
        <tr>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates select="TH"/>
        </tr>
    </xsl:template>

    <!-- Table row templates in body mode -->
    <xsl:template match="TR" mode="body" priority="10">
        <tr>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates select="TH|TD"/>
        </tr>
    </xsl:template>

    <!-- Table header cell template -->
    <xsl:template match="TH" priority="10">
        <th>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </th>
    </xsl:template>

    <!-- Table data cell template -->
    <xsl:template match="TD" priority="10">
        <td>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates/>
        </td>
    </xsl:template>

    <!-- Remove span elements from table cells (preserve content) -->
    <!-- This prevents numeric data from being wrapped in span elements -->
    <xsl:template match="TD/span | TH/span" priority="10">
        <xsl:apply-templates/>
    </xsl:template>

</xsl:stylesheet>
