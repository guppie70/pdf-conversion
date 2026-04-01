<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="3.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

    <!-- Remove empty paragraphs (no text and no images) -->
    <xsl:template match="p[not(normalize-space(translate(., '&#xA0;', ' '))) and not(.//img)]" priority="6"/>

    <!-- Remove empty inline formatting elements -->
    <xsl:template match="b[not(normalize-space(translate(., '&#xA0;', ' ')))]
                        | i[not(normalize-space(translate(., '&#xA0;', ' ')))]
                        | strong[not(normalize-space(translate(., '&#xA0;', ' ')))]
                        | em[not(normalize-space(translate(., '&#xA0;', ' ')))]" priority="6"/>

    <!-- Remove empty header elements -->
    <xsl:template match="*[matches(local-name(), '^h[1-6]$')]
                          [not(normalize-space(translate(., '&#xA0;', ' ')))]" priority="6"/>

    <!-- Strip <hr> elements -->
    <xsl:template match="hr" priority="6"/>

    <!-- Index all internal href="#..." references by their fragment identifier -->
    <xsl:key name="anchor-refs" match="a[starts-with(@href, '#')]" use="substring-after(@href, '#')"/>

    <!-- Remove orphaned anchor targets: no content, no matching href -->
    <xsl:template match="a[@name and not(node()) and not(key('anchor-refs', @name))]" priority="8"/>

    <!-- Clean <p> elements: strip class, style, id attributes -->
    <xsl:template match="p" priority="5">
        <p><xsl:apply-templates/></p>
    </xsl:template>

    <!-- Strip presentational attributes from remaining elements -->
    <xsl:template match="@style | @class | @align" priority="5"/>

    <!-- Whitespace normalization: collapse multiple spaces, convert non-breaking space -->
    <xsl:template match="text()" priority="3">
        <xsl:variable name="with-regular-spaces" select="translate(., '&#xA0;', ' ')"/>
        <xsl:if test="normalize-space($with-regular-spaces) != ''">
            <xsl:value-of select="replace($with-regular-spaces, '\s+', ' ')"/>
        </xsl:if>
    </xsl:template>

</xsl:stylesheet>
