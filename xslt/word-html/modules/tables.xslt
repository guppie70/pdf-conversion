<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:xs="http://www.w3.org/2001/XMLSchema"
    exclude-result-prefixes="xs">

    <!-- Table transformation: Word HTML → Taxxor TDM wrapper structure -->

    <xsl:template match="table" priority="10">
        <xsl:variable name="position" select="count(preceding::table) + 1"/>
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

                <!-- Word HTML has no thead/tbody — first row becomes header, rest becomes body -->
                <thead>
                    <xsl:apply-templates select="tr[1]" mode="table-header"/>
                </thead>
                <xsl:if test="tr[position() gt 1]">
                    <tbody>
                        <xsl:apply-templates select="tr[position() gt 1]" mode="table-body"/>
                    </tbody>
                </xsl:if>
            </table>
        </div>
    </xsl:template>

    <!-- Header row: process only td/th children (skips stray spans) -->
    <xsl:template match="tr" mode="table-header" priority="10">
        <tr>
            <xsl:apply-templates select="td | th" mode="table-header"/>
        </tr>
    </xsl:template>

    <!-- Body row: process only td/th children (skips stray spans) -->
    <xsl:template match="tr" mode="table-body" priority="10">
        <tr>
            <xsl:apply-templates select="td | th" mode="table-body"/>
        </tr>
    </xsl:template>

    <!-- Header cells: td → th, flatten content to text -->
    <xsl:template match="td | th" mode="table-header" priority="10">
        <th>
            <xsl:apply-templates select="@colspan | @rowspan"/>
            <xsl:choose>
                <xsl:when test="normalize-space(.) = ''">
                    <xsl:text>&#160;</xsl:text>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:value-of select="normalize-space(string-join(.//text(), ' '))"/>
                </xsl:otherwise>
            </xsl:choose>
        </th>
    </xsl:template>

    <!-- Body cells: flatten content to text -->
    <xsl:template match="td" mode="table-body" priority="10">
        <td>
            <xsl:apply-templates select="@colspan | @rowspan"/>
            <xsl:choose>
                <xsl:when test="normalize-space(.) = ''">
                    <xsl:text>&#160;</xsl:text>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:value-of select="normalize-space(string-join(.//text(), ' '))"/>
                </xsl:otherwise>
            </xsl:choose>
        </td>
    </xsl:template>

    <xsl:template match="th" mode="table-body" priority="10">
        <th>
            <xsl:apply-templates select="@colspan | @rowspan"/>
            <xsl:choose>
                <xsl:when test="normalize-space(.) = ''">
                    <xsl:text>&#160;</xsl:text>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:value-of select="normalize-space(string-join(.//text(), ' '))"/>
                </xsl:otherwise>
            </xsl:choose>
        </th>
    </xsl:template>

</xsl:stylesheet>
