<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

    <!-- Convert Wingdings symbol font spans to Unicode equivalents.
         Word uses font-family:Wingdings for bullet/checkmark characters (e.g. ü = ✓).
         Must run before generic span stripping (priority 8 > 5). -->
    <xsl:template match="span[contains(@style, 'Wingdings') or contains(@style, 'wingdings')]" priority="8">
        <xsl:variable name="char" select="normalize-space(translate(., '&#xA0;', ' '))"/>
        <xsl:choose>
            <xsl:when test="$char = '&#xFC;'">&#x2713; </xsl:when><!-- ü → ✓ checkmark -->
            <xsl:when test="$char = '&#xA7;'">&#x2022; </xsl:when><!-- § → ● bullet -->
            <xsl:when test="$char = '&#x6C;'">&#x2022; </xsl:when><!-- l → ● bullet -->
            <xsl:when test="$char = '&#x6E;'">&#x25A0; </xsl:when><!-- n → ■ square -->
            <xsl:when test="$char = '&#xD8;'">&#x2192; </xsl:when><!-- Ø → → arrow -->
            <xsl:when test="$char = '&#x76;'">&#x2714; </xsl:when><!-- v → ✔ heavy check -->
            <xsl:otherwise><xsl:value-of select="$char"/><xsl:text> </xsl:text></xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <!-- Bullet/number text before Word spacing spans: append trailing space.
         Saxon strips standalone whitespace-only text nodes in result trees,
         so the space must be part of the bullet text node to survive. -->
    <xsl:template match="text()[normalize-space(translate(., '&#xA0;', ' ')) != '']
                                [string-length(normalize-space(translate(., '&#xA0;', ' '))) &lt;= 3]
                                [following-sibling::*[1][contains(@style, '7.0pt') or contains(@style, '7pt')]]" priority="5">
        <xsl:value-of select="concat(normalize-space(translate(., '&#xA0;', ' ')), ' ')"/>
    </xsl:template>

    <!-- Strip all <span> elements, keeping their content -->
    <xsl:template match="span" priority="5">
        <xsl:apply-templates/>
    </xsl:template>

    <!-- Remove empty paragraphs (no text and no images) -->
    <xsl:template match="p[not(normalize-space(translate(., '&#xA0;', ' '))) and not(.//img)]" priority="6"/>

    <!-- Strip all <div> elements, keeping their content (same as span stripping) -->
    <xsl:template match="div" priority="5">
        <xsl:apply-templates/>
    </xsl:template>

    <!-- Remove empty inline formatting elements -->
    <xsl:template match="b[not(normalize-space(translate(., '&#xA0;', ' ')))]" priority="6"/>

    <!-- Remove empty header elements -->
    <xsl:template match="*[matches(local-name(), '^h[1-6]$')]
                          [not(normalize-space(translate(., '&#xA0;', ' ')))]" priority="6"/>

    <!-- Strip <hr> elements -->
    <xsl:template match="hr" priority="6"/>

    <!-- Clean <p> elements: strip class and style attributes -->
    <xsl:template match="p" priority="5">
        <p><xsl:apply-templates/></p>
    </xsl:template>

    <!-- Strip Word HTML presentational attributes -->
    <xsl:template match="@style | @clear | @align | @start | @type | @class | @width | @height" priority="5"/>

    <!-- Drop whitespace-only text nodes (including &#160; non-breaking spaces from Word), collapse whitespace in others -->
    <xsl:template match="text()" priority="3">
        <xsl:variable name="with-regular-spaces" select="translate(., '&#xA0;', ' ')"/>
        <xsl:if test="normalize-space($with-regular-spaces) != ''">
            <xsl:value-of select="replace($with-regular-spaces, '\s+', ' ')"/>
        </xsl:if>
    </xsl:template>

</xsl:stylesheet>
