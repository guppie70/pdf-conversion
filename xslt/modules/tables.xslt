<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns="http://www.w3.org/1999/xhtml"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                exclude-result-prefixes="xs">

    <!-- Table transformation templates (default mode) - Adobe PDF → Taxxor TDM structure -->

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
                    <xsl:when test="TR[1][TH and not(TD)]">
                        <thead>
                            <xsl:apply-templates select="TR[TH and not(TD)]" mode="table-header"/>
                        </thead>
                        <tbody>
                            <xsl:apply-templates select="TR[TD]" mode="table-body"/>
                        </tbody>
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
