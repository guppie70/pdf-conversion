<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                exclude-result-prefixes="xs">

    <!-- Table transformation templates (default mode) - Docling HTML → Taxxor TDM structure -->

    <xsl:template match="table" priority="10">
        <!-- Generate obfuscated but deterministic ID: d{pos*1677}e{pos*846} resembles original generate-id() format while remaining position-based -->
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
                <xsl:apply-templates select="@*"/>

                <!-- Process table structure: thead/tbody or all rows in tbody -->
                <xsl:choose>
                    <!-- Table has explicit thead/tbody structure -->
                    <xsl:when test="thead or tbody">
                        <xsl:apply-templates select="thead" mode="table-structure"/>
                        <xsl:choose>
                            <xsl:when test="tbody">
                                <xsl:apply-templates select="tbody" mode="table-structure"/>
                            </xsl:when>
                            <xsl:otherwise>
                                <tbody>
                                    <xsl:apply-templates select="tr" mode="table-body"/>
                                </tbody>
                            </xsl:otherwise>
                        </xsl:choose>
                    </xsl:when>

                    <!-- Table has direct tr children: detect header rows -->
                    <xsl:otherwise>
                        <xsl:choose>
                            <!-- First row has only th cells → create thead -->
                            <xsl:when test="tr[1][th and not(td)]">
                                <thead>
                                    <xsl:apply-templates select="tr[th and not(td)]" mode="table-header"/>
                                </thead>
                                <xsl:if test="tr[td]">
                                    <tbody>
                                        <xsl:apply-templates select="tr[td]" mode="table-body"/>
                                    </tbody>
                                </xsl:if>
                            </xsl:when>

                            <!-- All rows in tbody -->
                            <xsl:otherwise>
                                <tbody>
                                    <xsl:apply-templates select="tr" mode="table-body"/>
                                </tbody>
                            </xsl:otherwise>
                        </xsl:choose>
                    </xsl:otherwise>
                </xsl:choose>
            </table>
        </div>
    </xsl:template>

    <!-- Table structure mode: copy thead/tbody elements -->
    <xsl:template match="thead" mode="table-structure">
        <thead>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates select="tr" mode="table-header"/>
        </thead>
    </xsl:template>

    <xsl:template match="tbody" mode="table-structure">
        <tbody>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates select="tr" mode="table-body"/>
        </tbody>
    </xsl:template>

    <!-- Table row templates in table-header mode -->
    <xsl:template match="tr" mode="table-header" priority="10">
        <tr>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates select="th | td" mode="table-header"/>
        </tr>
    </xsl:template>

    <!-- Table row templates in table-body mode -->
    <xsl:template match="tr" mode="table-body" priority="10">
        <tr>
            <xsl:apply-templates select="@*"/>
            <xsl:apply-templates select="th | td" mode="table-body"/>
        </tr>
    </xsl:template>

    <!-- Table header cells -->
    <xsl:template match="th" mode="table-header" priority="10">
        <th>
            <xsl:apply-templates select="@*"/>
            <xsl:choose>
                <xsl:when test="normalize-space(.) = ''">
                    <xsl:text>&#160;</xsl:text>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:apply-templates/>
                </xsl:otherwise>
            </xsl:choose>
        </th>
    </xsl:template>

    <xsl:template match="td" mode="table-header" priority="10">
        <td>
            <xsl:apply-templates select="@*"/>
            <xsl:choose>
                <xsl:when test="normalize-space(.) = ''">
                    <xsl:text>&#160;</xsl:text>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:apply-templates/>
                </xsl:otherwise>
            </xsl:choose>
        </td>
    </xsl:template>

    <!-- Table body cells -->
    <xsl:template match="td" mode="table-body" priority="10">
        <td>
            <xsl:apply-templates select="@*"/>
            <xsl:choose>
                <xsl:when test="normalize-space(.) = ''">
                    <xsl:text>&#160;</xsl:text>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:apply-templates/>
                </xsl:otherwise>
            </xsl:choose>
        </td>
    </xsl:template>

    <xsl:template match="th" mode="table-body" priority="10">
        <th>
            <xsl:apply-templates select="@*"/>
            <xsl:choose>
                <xsl:when test="normalize-space(.) = ''">
                    <xsl:text>&#160;</xsl:text>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:apply-templates/>
                </xsl:otherwise>
            </xsl:choose>
        </th>
    </xsl:template>

</xsl:stylesheet>
