<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns="http://www.w3.org/1999/xhtml"
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
    <xsl:template match="*[local-name()='p']" mode="pass2">
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
    <xsl:template match="*[local-name()='p'][normalize-space(.) = '(continued)']"
                  mode="pass2"
                  priority="15"/>

    <!-- Suppress list items ending with "(continued)" (same pattern as paragraphs) -->
    <xsl:template match="*[local-name()='li' or (local-name()='a' and @href='#')]" mode="pass2">
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
    <xsl:template match="*[local-name()='li'][normalize-space(.) = '(continued)']"
                  mode="pass2"
                  priority="15"/>

</xsl:stylesheet>
