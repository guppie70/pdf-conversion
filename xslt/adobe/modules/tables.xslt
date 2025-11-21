<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                exclude-result-prefixes="xs">

    <!-- Table transformation templates (default mode) - Adobe PDF → Taxxor TDM structure -->

    <xsl:template match="Table" priority="10">
        <!-- Generate obfuscated but deterministic ID: d{pos*1677}e{pos*846} resembles original generate-id() format while remaining position-based -->
        <xsl:variable name="position" select="count(preceding::Table) + 1"/>
        <xsl:variable name="tableId" select="concat('d', $position * 1677, 'e', $position * 846)"/>
        <div id="tablewrapper_{$tableId}"
             class="table-wrapper c-table structured-data-table"
             data-instanceid="{$tableId}-wrapper">
            <div class="tablegraph-header-wrapper">
                <div class="table-title">tabletitle</div>
                <div class="table-scale">scale</div>
            </div>
            <table id="table_{$tableId}"
                   class="tabletype-numbers"
                   data-instanceid="{$tableId}-table">
                <xsl:apply-templates select="@*"/>
                <xsl:choose>
                    <xsl:when test="TR[1][TH and not(TD)]">
                        <thead>
                            <xsl:apply-templates select="TR[TH and not(TD)]" mode="table-header"/>
                        </thead>
                        <xsl:if test="TR[TD]">
                            <tbody>
                                <xsl:apply-templates select="TR[TD]" mode="table-body"/>
                            </tbody>
                        </xsl:if>
                    </xsl:when>
                    <xsl:otherwise>
                        <tbody>
                            <xsl:apply-templates select="TR" mode="table-body"/>
                        </tbody>
                    </xsl:otherwise>
                </xsl:choose>
            </table>
        </div>
    </xsl:template>

    <!-- Table row templates in table-header mode -->
    <xsl:template match="TR" mode="table-header" priority="10">
        <tr>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates select="TH" mode="table-header"/>
        </tr>
    </xsl:template>

    <!-- Table row templates in table-body mode -->
    <xsl:template match="TR" mode="table-body" priority="10">
        <tr>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates select="TH|TD" mode="table-body"/>
        </tr>
    </xsl:template>

    <!-- Table header cell - unwraps single <P> to avoid unnecessary wrappers -->
    <xsl:template match="TH" mode="table-header" priority="10">
        <th>
            <xsl:apply-templates select="@*"/>
            <xsl:choose>
                <xsl:when test="count(P[normalize-space(.) != '']) = 1 and
                                not(*[not(self::P or self::Artifact)])">
                    <xsl:variable name="singleP" select="P[normalize-space(.) != '']"/>
                    <xsl:apply-templates select="$singleP/@xml:lang"/>
                    <xsl:apply-templates select="$singleP/node()"/>
                </xsl:when>
                <xsl:when test="normalize-space(.) = ''">
                    <xsl:text>&#160;</xsl:text>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:apply-templates/>
                </xsl:otherwise>
            </xsl:choose>
        </th>
    </xsl:template>

    <!-- Table data cell - unwraps single <P> to avoid unnecessary wrappers -->
    <xsl:template match="TD" mode="table-body" priority="10">
        <td>
            <xsl:apply-templates select="@*"/>
            <xsl:choose>
                <!-- Single non-empty P element (ignoring Artifact): render P's content directly -->
                <xsl:when test="count(P[normalize-space(.) != '']) = 1 and
                                not(*[not(self::P or self::Artifact)])">
                    <xsl:variable name="singleP" select="P[normalize-space(.) != '']"/>
                    <!-- Preserve xml:lang and other attributes from P on the td element -->
                    <xsl:apply-templates select="$singleP/@xml:lang"/>
                    <!-- Render P's content without p wrapper -->
                    <xsl:apply-templates select="$singleP/node()"/>
                </xsl:when>
                <!-- Empty cell: insert non-breaking space to prevent self-closing -->
                <xsl:when test="normalize-space(.) = ''">
                    <xsl:text>&#160;</xsl:text>
                </xsl:when>
                <!-- Multiple P elements or mixed content: normal processing -->
                <xsl:otherwise>
                    <xsl:apply-templates/>
                </xsl:otherwise>
            </xsl:choose>
        </td>
    </xsl:template>

    <!-- Table header cell template in table-body mode (row headers) -->
    <xsl:template match="TH" mode="table-body" priority="10">
        <th>
            <xsl:apply-templates select="@*"/>
            <xsl:choose>
                <xsl:when test="count(P[normalize-space(.) != '']) = 1 and
                                not(*[not(self::P or self::Artifact)])">
                    <xsl:variable name="singleP" select="P[normalize-space(.) != '']"/>
                    <xsl:apply-templates select="$singleP/@xml:lang"/>
                    <xsl:apply-templates select="$singleP/node()"/>
                </xsl:when>
                <xsl:when test="normalize-space(.) = ''">
                    <xsl:text>&#160;</xsl:text>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:apply-templates/>
                </xsl:otherwise>
            </xsl:choose>
        </th>
    </xsl:template>

    <!-- Remove span elements from table cells (preserve content) -->
    <!-- This prevents numeric data from being wrapped in span elements -->
    <xsl:template match="TD/span | TH/span" priority="10">
        <xsl:apply-templates/>
    </xsl:template>

</xsl:stylesheet>
