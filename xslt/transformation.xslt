<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                xmlns:x="adobe:ns:meta/"
                xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                exclude-result-prefixes="xs x rdf">

    <xsl:output method="xhtml"
                encoding="UTF-8"
                indent="yes"
                omit-xml-declaration="no"/>

    <!-- Root template -->
    <xsl:template match="/">
        <html xmlns="http://www.w3.org/1999/xhtml">
            <head>
                <meta charset="UTF-8"/>
                <title>Taxxor TDM Document</title>
            </head>
            <body>
                <div class="document-content">
                    <xsl:apply-templates select="//Document"/>
                </div>
            </body>
        </html>
    </xsl:template>

    <!-- Document template -->
    <xsl:template match="Document">
        <xsl:apply-templates/>
    </xsl:template>

    <!-- Header templates -->
    <xsl:template match="H1">
        <h1><xsl:apply-templates/></h1>
    </xsl:template>

    <xsl:template match="H2">
        <h2><xsl:apply-templates/></h2>
    </xsl:template>

    <xsl:template match="H3">
        <h3><xsl:apply-templates/></h3>
    </xsl:template>

    <!-- Paragraph template -->
    <xsl:template match="P">
        <p>
            <xsl:copy-of select="@xml:lang"/>
            <xsl:apply-templates/>
        </p>
    </xsl:template>

    <!-- Table template with Taxxor structure -->
    <xsl:template match="Table">
        <xsl:variable name="tableId" select="generate-id()"/>
        <div id="tablewrapper_{$tableId}"
             class="table-wrapper structured-data-table"
             data-instanceid="{generate-id()}-wrapper">
            <div class="tablegraph-header-wrapper">
                <div class="table-title">tabletitle</div>
                <div class="table-scale">scale</div>
            </div>
            <table id="table_{$tableId}"
                   class="tabletype-numbers"
                   data-instanceid="{generate-id()}-table">
                <xsl:choose>
                    <!-- If first row contains TH elements, treat as header -->
                    <xsl:when test="TR[1]/TH">
                        <thead>
                            <xsl:apply-templates select="TR[TH]" mode="header"/>
                        </thead>
                        <tbody>
                            <xsl:apply-templates select="TR[TD]" mode="body"/>
                        </tbody>
                    </xsl:when>
                    <xsl:otherwise>
                        <tbody>
                            <xsl:apply-templates select="TR" mode="body"/>
                        </tbody>
                    </xsl:otherwise>
                </xsl:choose>
            </table>
        </div>
    </xsl:template>

    <!-- Table row templates -->
    <xsl:template match="TR" mode="header">
        <tr>
            <xsl:apply-templates select="TH"/>
        </tr>
    </xsl:template>

    <xsl:template match="TR" mode="body">
        <tr>
            <xsl:apply-templates select="TD"/>
        </tr>
    </xsl:template>

    <!-- Table cell templates -->
    <xsl:template match="TH">
        <th><xsl:apply-templates/></th>
    </xsl:template>

    <xsl:template match="TD">
        <td><xsl:apply-templates/></td>
    </xsl:template>

    <!-- List templates -->
    <xsl:template match="L">
        <xsl:choose>
            <xsl:when test="@ListType='Ordered'">
                <ol><xsl:apply-templates/></ol>
            </xsl:when>
            <xsl:otherwise>
                <ul><xsl:apply-templates/></ul>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <xsl:template match="LI">
        <li><xsl:apply-templates/></li>
    </xsl:template>

    <!-- Remove unnecessary elements -->
    <xsl:template match="Artifact | bookmark-tree | x:xmpmeta | rdf:RDF"/>

    <!-- Remove span elements from table cells (preserve content) -->
    <xsl:template match="TD/span | TH/span">
        <xsl:apply-templates/>
    </xsl:template>

    <!-- Default text node handling -->
    <xsl:template match="text()">
        <xsl:choose>
            <xsl:when test="normalize-space(.) = ''"/>
            <xsl:otherwise>
                <xsl:value-of select="normalize-space(.)"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <!-- Pass through elements not matched by other templates -->
    <xsl:template match="*">
        <xsl:apply-templates/>
    </xsl:template>

</xsl:stylesheet>