<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns="http://www.w3.org/1999/xhtml"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                exclude-result-prefixes="xs">

    <!-- ============================================================ -->
    <!-- POST-PROCESSING TEMPLATES (PASS 2 - mode="pass2")            -->
    <!-- ============================================================ -->
    <!-- This module performs post-processing cleanup on the          -->
    <!-- intermediate XHTML output from Pass 1.                       -->
    <!--                                                              -->
    <!-- Primary Purpose: Remove "(continued)" artifacts              -->
    <!--                                                              -->
    <!-- Why separate pass?                                           -->
    <!-- - "(continued)" appears in mixed content with Reference      -->
    <!-- - Text normalization happens during template processing      -->
    <!-- - Single-pass matching cannot reliably catch all patterns    -->
    <!-- - Post-processing on structured output is more reliable      -->
    <!--                                                              -->
    <!-- Elements filtered:                                           -->
    <!-- 1. Paragraphs (<p>) ending with "(continued)"                -->
    <!-- 2. List items (<li>) ending with "(continued)"               -->
    <!-- 3. Elements containing only "(continued)"                    -->
    <!-- 4. Case-insensitive matching                                 -->
    <!-- 5. Trailing/leading whitespace variations                    -->
    <!--                                                              -->
    <!-- Common patterns matched:                                     -->
    <!-- - <p>(f)Employee benefits (continued)</p>                    -->
    <!-- - <li><a>(f)Employee benefits (continued)</a></li>           -->
    <!-- - <p>Some text (continued)</p>                               -->
    <!-- ============================================================ -->

    <!-- Identity transform for pass2 (copy everything by default) -->
    <xsl:template match="node() | @*" mode="pass2">
        <xsl:copy>
            <xsl:apply-templates select="node() | @*" mode="pass2"/>
        </xsl:copy>
    </xsl:template>

    <!-- Suppress paragraphs ending with "(continued)" in any form -->
    <!-- Uses regex pattern to match:                              -->
    <!-- - Optional whitespace before "(continued)"                -->
    <!-- - Case variations: (continued), (Continued), (CONTINUED)  -->
    <!-- - Optional whitespace/punctuation after                   -->
    <xsl:template match="*[local-name()='p']" mode="pass2">
        <xsl:variable name="full-text" select="normalize-space(string(.))"/>

        <!-- Check if text matches "(continued)" pattern using regex -->
        <xsl:variable name="has-continued"
                      select="matches($full-text, '\(continued\)\s*$', 'i')"/>

        <!-- Only output the paragraph if it doesn't end with "(continued)" -->
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

    <!-- Suppress list items ending with "(continued)" in any form -->
    <!-- Handles TOC items like: <li><a>(f)Employee benefits (continued)</a></li> -->
    <!-- Uses same regex pattern as paragraphs above -->
    <xsl:template match="*[local-name()='li' or (local-name()='a' and @href='#')]" mode="pass2">
        <xsl:variable name="full-text" select="normalize-space(string(.))"/>

        <!-- Check if text matches "(continued)" pattern using regex -->
        <xsl:variable name="has-continued"
                      select="matches($full-text, '\(continued\)\s*$', 'i')"/>

        <!-- Only output the list item if it doesn't end with "(continued)" -->
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
