<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="3.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

    <!-- Image path rewriting for Word Plugin pipeline.
         Rewrites img src to Taxxor DM asset paths.
         {projectid} is a Taxxor DM runtime token resolved at render time. -->

    <xsl:template match="img" priority="10">
        <xsl:variable name="original-src" select="@src"/>
        <xsl:variable name="filename">
            <xsl:choose>
                <xsl:when test="contains($original-src, 'from-conversion/')">
                    <xsl:value-of select="substring-after($original-src, 'from-conversion/')"/>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:value-of select="$original-src"/>
                </xsl:otherwise>
            </xsl:choose>
        </xsl:variable>
        <xsl:variable name="taxxor-path"
            select="concat('/dataserviceassets/{projectid}/images/from-conversion/', $filename)"/>
        <img src="{$taxxor-path}">
            <xsl:copy-of select="@*[not(local-name()=('src','width','height','style','class','align'))]"/>
            <xsl:if test="not(@alt)">
                <xsl:attribute name="alt"/>
            </xsl:if>
        </img>
    </xsl:template>

</xsl:stylesheet>
