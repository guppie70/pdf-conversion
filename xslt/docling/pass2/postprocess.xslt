<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                xmlns:local="http://taxxor.com/xslt/local"
                exclude-result-prefixes="xs local">

    <!-- Pass 2: Post-processing cleanup and asymmetric row detection -->

    <!-- Identity transform for pass2 (copy everything by default) -->
    <xsl:template match="node() | @*" mode="pass2">
        <xsl:copy>
            <xsl:apply-templates select="node() | @*" mode="pass2"/>
        </xsl:copy>
    </xsl:template>

    <!-- Pattern 1: Handle table wrapper div - extract title from first tbody row if it has colspan -->
    <xsl:template match="div[contains(@class, 'table-wrapper')]" mode="pass2" priority="20">
        <xsl:variable name="first-tbody-row" select=".//tbody/tr[1]"/>
        <xsl:variable name="is-title-row" select="count($first-tbody-row/*) = 1 and $first-tbody-row/*[1]/@colspan"/>
        <xsl:variable name="table-title" select="if ($is-title-row) then normalize-space($first-tbody-row/*[1]) else ''"/>

        <xsl:copy>
            <xsl:apply-templates select="@*" mode="pass2"/>

            <!-- Process tablegraph-header-wrapper: inject title if found -->
            <xsl:apply-templates select="div[@class='tablegraph-header-wrapper']" mode="pass2-inject-title">
                <xsl:with-param name="title" select="$table-title"/>
            </xsl:apply-templates>

            <!-- Process table element (will handle tbody row removal) -->
            <xsl:apply-templates select="table" mode="pass2"/>
        </xsl:copy>
    </xsl:template>

    <!-- Inject extracted title into table-title div -->
    <xsl:template match="div[@class='tablegraph-header-wrapper']" mode="pass2-inject-title">
        <xsl:param name="title" select="''"/>
        <xsl:copy>
            <xsl:apply-templates select="@*" mode="pass2"/>
            <div class="table-title">
                <xsl:value-of select="if ($title != '') then $title else 'tabletitle'"/>
            </div>
            <xsl:apply-templates select="div[@class='table-scale']" mode="pass2"/>
        </xsl:copy>
    </xsl:template>

    <!-- Function to calculate effective cell count including colspan/rowspan -->
    <xsl:function name="local:effective-cell-count" as="xs:integer">
        <xsl:param name="row" as="element(tr)"/>
        <xsl:value-of select="sum($row/(td|th)/(if (@colspan castable as xs:integer) then xs:integer(@colspan) else 1))"/>
    </xsl:function>

    <!-- Detect asymmetric table rows: mark rows with fewer cells than table maximum -->
    <xsl:template match="table" mode="pass2" priority="10">
        <!-- Calculate max cell count across all rows (accounting for colspan) -->
        <xsl:variable name="max-cells" as="xs:integer">
            <xsl:choose>
                <xsl:when test=".//tr">
                    <xsl:value-of select="max(.//tr/local:effective-cell-count(.))"/>
                </xsl:when>
                <xsl:otherwise>0</xsl:otherwise>
            </xsl:choose>
        </xsl:variable>

        <xsl:copy>
            <xsl:apply-templates select="@*" mode="pass2"/>

            <!-- Process thead if exists -->
            <xsl:apply-templates select="thead" mode="pass2-table">
                <xsl:with-param name="max-cells" select="$max-cells" tunnel="yes"/>
            </xsl:apply-templates>

            <!-- Process tbody: detect and move header rows to thead if needed -->
            <xsl:apply-templates select="tbody" mode="pass2-tbody">
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
        <xsl:variable name="current-cells" select="local:effective-cell-count(.)"/>

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

    <!-- Helper function: check if row is a header row (only th OR first td then th) -->
    <xsl:function name="local:is-header-row" as="xs:boolean">
        <xsl:param name="row" as="element(tr)"/>
        <xsl:choose>
            <!-- Row contains only th elements -->
            <xsl:when test="$row[th and not(td)]">
                <xsl:value-of select="true()"/>
            </xsl:when>
            <!-- Row starts with td but all other cells are th -->
            <xsl:when test="$row/td[1] and $row/*[position() gt 1 and self::td]">
                <xsl:value-of select="false()"/>
            </xsl:when>
            <xsl:when test="$row/td[1] and $row/*[position() gt 1]/self::th">
                <xsl:value-of select="true()"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="false()"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:function>

    <!-- Recursive template: collect consecutive header rows -->
    <xsl:template name="collect-consecutive-header-rows">
        <xsl:param name="rows" as="element(tr)*"/>
        <xsl:param name="max-rows" as="xs:integer"/>
        <xsl:param name="collected" as="xs:integer" select="0"/>

        <xsl:if test="$collected lt $max-rows and exists($rows[1]) and local:is-header-row($rows[1])">
            <xsl:sequence select="$rows[1]"/>
            <xsl:call-template name="collect-consecutive-header-rows">
                <xsl:with-param name="rows" select="$rows[position() gt 1]"/>
                <xsl:with-param name="max-rows" select="$max-rows"/>
                <xsl:with-param name="collected" select="$collected + 1"/>
            </xsl:call-template>
        </xsl:if>
    </xsl:template>

    <!-- Process tbody: detect header rows and move to thead, handle title row -->
    <xsl:template match="tbody" mode="pass2-tbody" priority="10">
        <xsl:param name="max-cells" as="xs:integer" tunnel="yes"/>

        <xsl:variable name="table-has-more-than-two-rows" select="count(tr) gt 2"/>
        <xsl:variable name="first-row" select="tr[1]"/>

        <!-- Pattern 1: Check if first row is a title row (single cell with colspan) -->
        <xsl:variable name="is-title-row" select="count($first-row/*) = 1 and $first-row/*[1]/@colspan"/>
        <xsl:variable name="title-offset" select="if ($is-title-row) then 1 else 0"/>

        <!-- Pattern 2: Identify consecutive header rows (after title, stop at first non-header) -->
        <xsl:variable name="header-rows" as="element(tr)*">
            <xsl:if test="$table-has-more-than-two-rows">
                <xsl:call-template name="collect-consecutive-header-rows">
                    <xsl:with-param name="rows" select="tr[position() gt $title-offset]"/>
                    <xsl:with-param name="max-rows" select="3"/>
                </xsl:call-template>
            </xsl:if>
        </xsl:variable>

        <xsl:variable name="num-header-rows" select="count($header-rows)"/>

        <!-- Create or augment thead if header rows detected -->
        <xsl:if test="$num-header-rows gt 0">
            <xsl:choose>
                <!-- Merge with existing thead -->
                <xsl:when test="../thead">
                    <thead>
                        <xsl:apply-templates select="../thead/@*" mode="pass2-table"/>
                        <xsl:apply-templates select="../thead/tr" mode="pass2-table">
                            <xsl:with-param name="max-cells" select="$max-cells" tunnel="yes"/>
                        </xsl:apply-templates>
                        <xsl:apply-templates select="$header-rows" mode="pass2-table">
                            <xsl:with-param name="max-cells" select="$max-cells" tunnel="yes"/>
                        </xsl:apply-templates>
                    </thead>
                </xsl:when>
                <!-- Create new thead -->
                <xsl:otherwise>
                    <thead>
                        <xsl:apply-templates select="$header-rows" mode="pass2-table">
                            <xsl:with-param name="max-cells" select="$max-cells" tunnel="yes"/>
                        </xsl:apply-templates>
                    </thead>
                </xsl:otherwise>
            </xsl:choose>
        </xsl:if>

        <!-- Process tbody: skip title row and header rows -->
        <tbody>
            <xsl:apply-templates select="@*" mode="pass2-table"/>
            <xsl:apply-templates select="tr[position() gt ($title-offset + $num-header-rows)]" mode="pass2-table">
                <xsl:with-param name="max-cells" select="$max-cells" tunnel="yes"/>
            </xsl:apply-templates>
        </tbody>
    </xsl:template>

    <!-- Cell normalization: ensure thead uses th, tbody uses td -->

    <!-- Convert td to th when inside thead -->
    <xsl:template match="thead//td" mode="pass2-table" priority="15">
        <th>
            <xsl:apply-templates select="@* | node()" mode="pass2-table"/>
        </th>
    </xsl:template>

    <!-- Keep th as th when inside thead -->
    <xsl:template match="thead//th" mode="pass2-table" priority="15">
        <xsl:copy>
            <xsl:apply-templates select="@* | node()" mode="pass2-table"/>
        </xsl:copy>
    </xsl:template>

    <!-- Convert th to td when inside tbody -->
    <xsl:template match="tbody//th" mode="pass2-table" priority="15">
        <td>
            <xsl:apply-templates select="@* | node()" mode="pass2-table"/>
        </td>
    </xsl:template>

    <!-- Keep td as td when inside tbody -->
    <xsl:template match="tbody//td" mode="pass2-table" priority="15">
        <xsl:copy>
            <xsl:apply-templates select="@* | node()" mode="pass2-table"/>
        </xsl:copy>
    </xsl:template>

</xsl:stylesheet>
