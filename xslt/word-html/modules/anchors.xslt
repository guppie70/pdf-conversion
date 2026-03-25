<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

    <!-- Index all internal href="#..." references by their fragment identifier -->
    <xsl:key name="anchor-refs" match="a[starts-with(@href, '#')]" use="substring-after(@href, '#')"/>

    <!-- Remove empty orphaned anchor targets (no content, no matching href) -->
    <xsl:template match="a[@name and not(node()) and not(key('anchor-refs', @name))]" priority="8"/>

    <!-- Referenced empty anchors: keep as-is -->
    <xsl:template match="a[@name and not(node()) and key('anchor-refs', @name)]" priority="8">
        <a name="{@name}"/>
    </xsl:template>

    <!-- Orphaned anchors with content: strip the <a> wrapper, keep content -->
    <xsl:template match="a[@name and node() and not(@href) and not(key('anchor-refs', @name))]" priority="8">
        <xsl:apply-templates/>
    </xsl:template>

</xsl:stylesheet>
