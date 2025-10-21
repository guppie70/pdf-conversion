<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                exclude-result-prefixes="xs">

    <!-- Post-processing templates (Pass 2, mode="pass2") - Remove "(continued)" artifacts from <p> and <li> elements -->

    <!-- Identity transform for pass2 (copy everything by default) -->
    <xsl:template match="node() | @*" mode="pass2">
        <xsl:copy>
            <xsl:apply-templates select="node() | @*" mode="pass2"/>
        </xsl:copy>
    </xsl:template>

    <!-- Suppress paragraphs ending with "(continued)" (case-insensitive, with optional whitespace) -->
    <xsl:template match="p" mode="pass2">
        <xsl:variable name="full-text" select="normalize-space(string(.))"/>

        <xsl:variable name="has-continued"
                      select="matches($full-text, '\(continued\)\s*$', 'i')"/>
        <xsl:if test="not($has-continued)">
            <xsl:copy>
                <xsl:apply-templates select="node() | @*" mode="pass2"/>
            </xsl:copy>
        </xsl:if>
    </xsl:template>

    <!-- Special handling for paragraphs containing ONLY "(continued)" -->
    <!-- Priority 15 ensures this template fires before the general one above -->
    <xsl:template match="p[normalize-space(.) = '(continued)']"
                  mode="pass2"
                  priority="15"/>

    <!-- Suppress list items ending with "(continued)" (same pattern as paragraphs) -->
    <xsl:template match="li | a[@href='#']" mode="pass2">
        <xsl:variable name="full-text" select="normalize-space(string(.))"/>

        <xsl:variable name="has-continued"
                      select="matches($full-text, '\(continued\)\s*$', 'i')"/>
        <xsl:if test="not($has-continued)">
            <xsl:copy>
                <xsl:apply-templates select="node() | @*" mode="pass2"/>
            </xsl:copy>
        </xsl:if>
    </xsl:template>

    <!-- Special handling for list items containing ONLY "(continued)" -->
    <!-- Priority 15 ensures this template fires before the general one above -->
    <xsl:template match="li[normalize-space(.) = '(continued)']"
                  mode="pass2"
                  priority="15"/>

    <!-- Detect and mark asymmetrical table rows -->
    <!-- Asymmetrical = row has fewer cells than the maximum number of cells in the table -->
    <xsl:template match="tr" mode="pass2" priority="10">
        <xsl:variable name="current-cell-count" select="count(td | th)"/>

        <!-- Find the maximum number of cells in any row of this table -->
        <xsl:variable name="max-cell-count">
            <xsl:for-each select="ancestor::table[1]//tr">
                <xsl:sort select="count(td | th)" data-type="number" order="descending"/>
                <xsl:if test="position() = 1">
                    <xsl:value-of select="count(td | th)"/>
                </xsl:if>
            </xsl:for-each>
        </xsl:variable>

        <xsl:copy>
            <!-- Mark as asymmetric if this row has fewer cells than the max -->
            <xsl:if test="$current-cell-count &lt; $max-cell-count">
                <xsl:attribute name="data-asymmetric">true</xsl:attribute>
                <xsl:attribute name="data-cell-count"><xsl:value-of select="$current-cell-count"/></xsl:attribute>
                <xsl:attribute name="data-expected-count"><xsl:value-of select="$max-cell-count"/></xsl:attribute>
            </xsl:if>
            <xsl:apply-templates select="@* | node()" mode="pass2"/>
        </xsl:copy>
    </xsl:template>

    <!-- Extract headers from list items: split lists when headers are found -->
    <xsl:template match="ul | ol" mode="pass2" priority="10">
        <!-- Check for headers as direct children OR nested descendants -->
        <xsl:variable name="has-headers" select="boolean(li//*[@data-extracted-header])"/>
        <xsl:choose>
            <!-- List contains items with extracted headers: split and extract -->
            <xsl:when test="$has-headers">
                <!-- Two-pass approach: 1) Collect all li items, 2) Extract headers -->

                <!-- Pass 1: Build list with all li items (regular + preceding content from header items) -->
                <xsl:variable name="has-list-items" select="exists(li[not(.//*[@data-extracted-header])]) or exists(li//*[@data-extracted-header]/preceding-sibling::node()[normalize-space(.) != ''])"/>

                <xsl:if test="$has-list-items">
                    <xsl:element name="{local-name(.)}">
                        <xsl:apply-templates select="@*" mode="pass2"/>

                        <!-- Process all li elements -->
                        <xsl:for-each select="li">
                            <xsl:choose>
                                <!-- Item contains a header: output only preceding content as li -->
                                <xsl:when test=".//*[@data-extracted-header]">
                                    <xsl:call-template name="output-preceding-content-only"/>
                                </xsl:when>
                                <!-- Regular item: output normally -->
                                <xsl:otherwise>
                                    <xsl:apply-templates select="." mode="pass2"/>
                                </xsl:otherwise>
                            </xsl:choose>
                        </xsl:for-each>
                    </xsl:element>
                </xsl:if>

                <!-- Pass 2: Extract all headers (output after the list) -->
                <xsl:for-each select="li[.//*[@data-extracted-header]]">
                    <xsl:call-template name="extract-header-only"/>
                </xsl:for-each>
            </xsl:when>
            <!-- No headers to extract: copy normally -->
            <xsl:otherwise>
                <xsl:copy>
                    <xsl:apply-templates select="@* | node()" mode="pass2"/>
                </xsl:copy>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <!-- Named template: extract header from li element (handles nested headers) -->
    <xsl:template name="extract-header-from-li">
        <!-- Find header anywhere in the li (direct child or nested) -->
        <xsl:variable name="header" select=".//*[@data-extracted-header][1]"/>

        <xsl:choose>
            <!-- Header is a direct child of li -->
            <xsl:when test="*[@data-extracted-header]">
                <xsl:variable name="direct-header" select="*[@data-extracted-header][1]"/>
                <xsl:variable name="header-element-name" select="local-name($direct-header)"/>
                <xsl:variable name="preceding-content" select="$direct-header/preceding-sibling::node()[normalize-space(.) != '']"/>
                <xsl:variable name="following-content" select="$direct-header/following-sibling::node()[normalize-space(.) != '']"/>

                <!-- Output preceding content in li if it exists -->
                <xsl:if test="exists($preceding-content)">
                    <li>
                        <xsl:apply-templates select="@*" mode="pass2"/>
                        <xsl:apply-templates select="$preceding-content" mode="pass2"/>
                    </li>
                </xsl:if>

                <!-- Output header without the extraction marker and debug attribute -->
                <xsl:element name="{$header-element-name}">
                    <xsl:for-each select="$direct-header">
                        <xsl:apply-templates select="@* except (@data-extracted-header, @data-debug-level)" mode="pass2"/>
                        <xsl:apply-templates select="node()" mode="pass2"/>
                    </xsl:for-each>
                </xsl:element>

                <!-- Output following content in li if it exists -->
                <xsl:if test="exists($following-content)">
                    <li>
                        <xsl:apply-templates select="@*" mode="pass2"/>
                        <xsl:apply-templates select="$following-content" mode="pass2"/>
                    </li>
                </xsl:if>
            </xsl:when>

            <!-- Header is nested inside li content -->
            <xsl:when test="$header">
                <xsl:variable name="nested-header-element-name" select="local-name($header)"/>

                <!-- Get all content before the header's ancestor (walk up to find what's before) -->
                <xsl:variable name="content-before-header">
                    <xsl:call-template name="get-content-before-header">
                        <xsl:with-param name="header" select="$header"/>
                        <xsl:with-param name="li-context" select="."/>
                    </xsl:call-template>
                </xsl:variable>

                <!-- Output preceding content as li if it exists -->
                <xsl:if test="normalize-space($content-before-header) != ''">
                    <li>
                        <xsl:apply-templates select="@*" mode="pass2"/>
                        <xsl:copy-of select="$content-before-header"/>
                    </li>
                </xsl:if>

                <!-- Output the header -->
                <xsl:element name="{$nested-header-element-name}">
                    <xsl:for-each select="$header">
                        <xsl:apply-templates select="@* except (@data-extracted-header, @data-debug-level)" mode="pass2"/>
                        <xsl:apply-templates select="node()" mode="pass2"/>
                    </xsl:for-each>
                </xsl:element>

                <!-- Get content after header -->
                <xsl:variable name="content-after-header">
                    <xsl:call-template name="get-content-after-header">
                        <xsl:with-param name="header" select="$header"/>
                        <xsl:with-param name="li-context" select="."/>
                    </xsl:call-template>
                </xsl:variable>

                <!-- Output following content as li if it exists -->
                <xsl:if test="normalize-space($content-after-header) != ''">
                    <li>
                        <xsl:apply-templates select="@*" mode="pass2"/>
                        <xsl:copy-of select="$content-after-header"/>
                    </li>
                </xsl:if>
            </xsl:when>
        </xsl:choose>
    </xsl:template>

    <!-- Helper: Output only preceding content from li with header (for Pass 1) -->
    <xsl:template name="output-preceding-content-only">
        <xsl:variable name="header" select=".//*[@data-extracted-header][1]"/>

        <xsl:choose>
            <!-- Header is a direct child of li -->
            <xsl:when test="*[@data-extracted-header]">
                <xsl:variable name="direct-header" select="*[@data-extracted-header][1]"/>
                <xsl:variable name="preceding-content" select="$direct-header/preceding-sibling::node()[normalize-space(.) != '']"/>

                <!-- Output preceding content in li if it exists -->
                <xsl:if test="exists($preceding-content)">
                    <li>
                        <xsl:apply-templates select="@*" mode="pass2"/>
                        <xsl:apply-templates select="$preceding-content" mode="pass2"/>
                    </li>
                </xsl:if>
            </xsl:when>

            <!-- Header is nested inside li content -->
            <xsl:when test="$header">
                <!-- Get all content before the header's ancestor -->
                <xsl:variable name="content-before-header">
                    <xsl:call-template name="get-content-before-header">
                        <xsl:with-param name="header" select="$header"/>
                        <xsl:with-param name="li-context" select="."/>
                    </xsl:call-template>
                </xsl:variable>

                <!-- Output preceding content as li if it exists -->
                <xsl:if test="normalize-space($content-before-header) != ''">
                    <li>
                        <xsl:apply-templates select="@*" mode="pass2"/>
                        <xsl:copy-of select="$content-before-header"/>
                    </li>
                </xsl:if>
            </xsl:when>
        </xsl:choose>
    </xsl:template>

    <!-- Helper: Output only extracted header (for Pass 2) -->
    <xsl:template name="extract-header-only">
        <xsl:variable name="header" select=".//*[@data-extracted-header][1]"/>

        <xsl:choose>
            <!-- Header is a direct child of li -->
            <xsl:when test="*[@data-extracted-header]">
                <xsl:variable name="direct-header" select="*[@data-extracted-header][1]"/>
                <xsl:variable name="header-element-name" select="local-name($direct-header)"/>

                <!-- Output header without the extraction marker -->
                <xsl:element name="{$header-element-name}">
                    <xsl:for-each select="$direct-header">
                        <xsl:apply-templates select="@* except (@data-extracted-header, @data-debug-level)" mode="pass2"/>
                        <xsl:apply-templates select="node()" mode="pass2"/>
                    </xsl:for-each>
                </xsl:element>
            </xsl:when>

            <!-- Header is nested inside li content -->
            <xsl:when test="$header">
                <xsl:variable name="nested-header-element-name" select="local-name($header)"/>

                <!-- Output the header -->
                <xsl:element name="{$nested-header-element-name}">
                    <xsl:for-each select="$header">
                        <xsl:apply-templates select="@* except (@data-extracted-header, @data-debug-level)" mode="pass2"/>
                        <xsl:apply-templates select="node()" mode="pass2"/>
                    </xsl:for-each>
                </xsl:element>
            </xsl:when>
        </xsl:choose>
    </xsl:template>

    <!-- Helper: Get content before nested header -->
    <xsl:template name="get-content-before-header">
        <xsl:param name="header"/>
        <xsl:param name="li-context"/>

        <!-- Find the topmost ancestor of the header that is a direct child of li -->
        <xsl:variable name="top-ancestor" select="$header/ancestor::*[parent::li][1]"/>

        <!-- Return all siblings before the top ancestor -->
        <xsl:if test="$top-ancestor">
            <xsl:apply-templates select="$top-ancestor/preceding-sibling::node()" mode="pass2"/>
        </xsl:if>
    </xsl:template>

    <!-- Helper: Get content after nested header -->
    <xsl:template name="get-content-after-header">
        <xsl:param name="header"/>
        <xsl:param name="li-context"/>

        <!-- Find the topmost ancestor of the header that is a direct child of li -->
        <xsl:variable name="top-ancestor" select="$header/ancestor::*[parent::li][1]"/>

        <!-- Return all siblings after the top ancestor -->
        <xsl:if test="$top-ancestor">
            <xsl:apply-templates select="$top-ancestor/following-sibling::node()" mode="pass2"/>
        </xsl:if>
    </xsl:template>

</xsl:stylesheet>
