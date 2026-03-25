<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                exclude-result-prefixes="xs">

    <!-- Table symmetry processing (Pass 3, mode="pass3") - Add missing cells to asymmetric rows -->

    <!-- Identity transform for pass3 (copy everything by default) -->
    <xsl:template match="node() | @*" mode="pass3">
        <xsl:copy>
            <xsl:apply-templates select="node() | @*" mode="pass3"/>
        </xsl:copy>
    </xsl:template>

    <!-- Template for asymmetric rows: add missing cells to balance the row -->
    <xsl:template match="tr[@data-asymmetric='true']" mode="pass3" priority="10">
        <xsl:variable name="current-cell-count" select="xs:integer(@data-cell-count)"/>
        <xsl:variable name="expected-cell-count" select="xs:integer(@data-expected-count)"/>
        <xsl:variable name="cells-to-add" select="$expected-cell-count - $current-cell-count"/>

        <!-- Get injection position from ancestor table element -->
        <xsl:variable name="inject-position-attr" select="ancestor::table/@data-injectcell-position"/>
        <xsl:variable name="inject-position" select="if ($inject-position-attr castable as xs:integer)
                                                       then xs:integer($inject-position-attr)
                                                       else 0"/>

        <!-- Validate injection position: must be between 1 and current cell count (inclusive) -->
        <xsl:variable name="valid-position" select="if ($inject-position ge 1 and $inject-position le $current-cell-count)
                                                      then $inject-position
                                                      else 0"/>

        <!-- Get current cells -->
        <xsl:variable name="current-cells" select="td | th"/>

        <!-- Copy tr element with attributes -->
        <xsl:copy>
            <xsl:apply-templates select="@*" mode="pass3"/>

            <xsl:choose>
                <!-- Inject cells at specific position -->
                <xsl:when test="$valid-position gt 0">
                    <!-- Process cells before injection point -->
                    <xsl:apply-templates select="$current-cells[position() lt $valid-position]" mode="pass3"/>

                    <!-- Insert new cells at injection point -->
                    <xsl:call-template name="create-missing-cells">
                        <xsl:with-param name="count" select="$cells-to-add"/>
                        <xsl:with-param name="element-name" select="if ($current-cells[position() = $valid-position - 1])
                                                                      then local-name($current-cells[position() = $valid-position - 1])
                                                                      else 'td'"/>
                    </xsl:call-template>

                    <!-- Process cells from injection point onwards -->
                    <xsl:apply-templates select="$current-cells[position() ge $valid-position]" mode="pass3"/>
                </xsl:when>

                <!-- Append cells at end (default behavior) -->
                <xsl:otherwise>
                    <!-- Process all existing cells -->
                    <xsl:apply-templates select="$current-cells" mode="pass3"/>

                    <!-- Append new cells -->
                    <xsl:call-template name="create-missing-cells">
                        <xsl:with-param name="count" select="$cells-to-add"/>
                        <xsl:with-param name="element-name" select="if ($current-cells[last()])
                                                                      then local-name($current-cells[last()])
                                                                      else 'td'"/>
                    </xsl:call-template>
                </xsl:otherwise>
            </xsl:choose>
        </xsl:copy>
    </xsl:template>

    <!-- Named template to create N missing cells -->
    <xsl:template name="create-missing-cells">
        <xsl:param name="count" as="xs:integer"/>
        <xsl:param name="element-name" as="xs:string"/>

        <xsl:if test="$count gt 0">
            <xsl:element name="{$element-name}">
                <xsl:attribute name="data-cell-added">true</xsl:attribute>
                <xsl:text>&#160;</xsl:text>
            </xsl:element>

            <!-- Recursively create remaining cells -->
            <xsl:if test="$count gt 1">
                <xsl:call-template name="create-missing-cells">
                    <xsl:with-param name="count" select="$count - 1"/>
                    <xsl:with-param name="element-name" select="$element-name"/>
                </xsl:call-template>
            </xsl:if>
        </xsl:if>
    </xsl:template>

    <!-- Merge adjacent same-name inline elements split by Word anchor boundaries.
         e.g. <u>Strategy</u><u> &amp; Activities</u> → <u>Strategy &amp; Activities</u> -->
    <xsl:template match="*[u[following-sibling::node()[1]/self::u] or
                            b[following-sibling::node()[1]/self::b] or
                            i[following-sibling::node()[1]/self::i]]" mode="pass3" priority="5">
        <xsl:copy>
            <xsl:apply-templates select="@*" mode="pass3"/>
            <xsl:for-each-group select="node()" group-adjacent="
                if (self::u) then 'u'
                else if (self::b) then 'b'
                else if (self::i) then 'i'
                else generate-id()">
                <xsl:choose>
                    <xsl:when test="current-grouping-key() = 'u'">
                        <span class="tx-underline">
                            <xsl:apply-templates select="current-group()/node()" mode="pass3"/>
                        </span>
                    </xsl:when>
                    <xsl:when test="current-grouping-key() = ('b', 'i')">
                        <xsl:element name="{current-grouping-key()}">
                            <xsl:apply-templates select="current-group()/node()" mode="pass3"/>
                        </xsl:element>
                    </xsl:when>
                    <xsl:otherwise>
                        <xsl:apply-templates select="current-group()" mode="pass3"/>
                    </xsl:otherwise>
                </xsl:choose>
            </xsl:for-each-group>
        </xsl:copy>
    </xsl:template>

    <!-- Single-row tables: convert lone thead (no tbody sibling) to tbody, th to td -->
    <xsl:template match="thead[not(../tbody)]" mode="pass3" priority="5">
        <tbody>
            <xsl:apply-templates select="@* | node()" mode="pass3"/>
        </tbody>
    </xsl:template>

    <xsl:template match="th[ancestor::thead[not(../tbody)]]" mode="pass3" priority="6">
        <td>
            <xsl:apply-templates select="@* | node()" mode="pass3"/>
        </td>
    </xsl:template>

    <!-- Convert <u> to <span class="tx-underline"> for Taxxor DM -->
    <xsl:template match="u" mode="pass3" priority="5">
        <span class="tx-underline">
            <xsl:apply-templates mode="pass3"/>
        </span>
    </xsl:template>

</xsl:stylesheet>
