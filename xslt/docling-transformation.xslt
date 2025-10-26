<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="3.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:xs="http://www.w3.org/2001/XMLSchema"
    exclude-result-prefixes="xs">

    <xsl:output method="xml" indent="yes" encoding="UTF-8" omit-xml-declaration="no"/>

    <!-- Global parameter for project ID (used in image paths) -->
    <xsl:param name="projectid" select="'default-project'"/>

    <!-- Root template -->
    <xsl:template match="/">
        <html>
            <head>
                <meta charset="UTF-8"/>
                <title>Taxxor TDM Document</title>
            </head>
            <body>
                <div class="document-content">
                    <xsl:apply-templates select="//body/node()"/>
                </div>
            </body>
        </html>
    </xsl:template>

    <!-- Remove style elements -->
    <xsl:template match="style"/>

    <!-- Remove page wrapper divs, process content -->
    <xsl:template match="div[@class='page']">
        <xsl:apply-templates select="node()"/>
    </xsl:template>

    <!-- Header transformations: h2 -> h1, h3 -> h2, h4 -> h3 -->
    <xsl:template match="h2">
        <h1>
            <xsl:call-template name="add-header-attributes"/>
            <xsl:apply-templates select="node()"/>
        </h1>
    </xsl:template>

    <xsl:template match="h3">
        <h2>
            <xsl:call-template name="add-header-attributes"/>
            <xsl:apply-templates select="node()"/>
        </h2>
    </xsl:template>

    <xsl:template match="h4">
        <h3>
            <xsl:call-template name="add-header-attributes"/>
            <xsl:apply-templates select="node()"/>
        </h3>
    </xsl:template>

    <!-- Named template to add optional header attributes -->
    <xsl:template name="add-header-attributes">
        <!-- Add data-numberscheme and data-number if needed in future -->
        <!-- <xsl:attribute name="data-numberscheme" select="''"/> -->
        <!-- <xsl:attribute name="data-number" select="''"/> -->
    </xsl:template>

    <!-- Paragraphs: keep as-is -->
    <xsl:template match="p">
        <p>
            <xsl:apply-templates select="node()"/>
        </p>
    </xsl:template>

    <!-- Tables: wrap in Taxxor structure -->
    <xsl:template match="table">
        <xsl:variable name="table-id" select="generate-id()"/>

        <div id="tablewrapper_{$table-id}"
             class="table-wrapper c-table structured-data-table"
             data-instanceid="{$table-id}-wrapper">

            <div class="tablegraph-header-wrapper">
                <div class="table-title">Table</div>
                <div class="table-scale">EUR thousands</div>
            </div>

            <table id="table_{$table-id}"
                   class="tabletype-numbers"
                   data-instanceid="{$table-id}-table">
                <xsl:apply-templates select="node()"/>
            </table>
        </div>
    </xsl:template>

    <!-- Table structure elements: keep as-is -->
    <xsl:template match="tbody | thead | tfoot">
        <xsl:copy>
            <xsl:apply-templates select="node()"/>
        </xsl:copy>
    </xsl:template>

    <xsl:template match="tr">
        <tr>
            <xsl:apply-templates select="node()"/>
        </tr>
    </xsl:template>

    <xsl:template match="td">
        <td>
            <xsl:apply-templates select="node()"/>
        </td>
    </xsl:template>

    <xsl:template match="th">
        <th>
            <xsl:apply-templates select="node()"/>
        </th>
    </xsl:template>

    <!-- Lists: map ol/ul, strip style attributes from li -->
    <xsl:template match="ol">
        <ol>
            <xsl:apply-templates select="node()"/>
        </ol>
    </xsl:template>

    <xsl:template match="ul">
        <ul>
            <xsl:apply-templates select="node()"/>
        </ul>
    </xsl:template>

    <xsl:template match="li">
        <li>
            <!-- Strip style attribute, keep content -->
            <xsl:apply-templates select="node()"/>
        </li>
    </xsl:template>

    <!-- Images: transform to Taxxor path pattern -->
    <xsl:template match="img">
        <img>
            <xsl:attribute name="src">
                <xsl:choose>
                    <xsl:when test="@src">
                        <!-- Extract filename from source path -->
                        <xsl:variable name="filename" select="tokenize(@src, '/')[last()]"/>
                        <xsl:value-of select="concat('/dataserviceassets/', $projectid, '/images/from-conversion/', $filename)"/>
                    </xsl:when>
                    <xsl:otherwise>
                        <xsl:value-of select="concat('/dataserviceassets/', $projectid, '/images/from-conversion/placeholder.png')"/>
                    </xsl:otherwise>
                </xsl:choose>
            </xsl:attribute>
            <xsl:if test="@alt">
                <xsl:attribute name="alt" select="@alt"/>
            </xsl:if>
        </img>
    </xsl:template>

    <!-- Figures: keep structure but transform child images -->
    <xsl:template match="figure">
        <figure>
            <xsl:apply-templates select="node()"/>
        </figure>
    </xsl:template>

    <xsl:template match="figcaption">
        <figcaption>
            <xsl:apply-templates select="node()"/>
        </figcaption>
    </xsl:template>

    <!-- Text formatting: preserve common inline elements -->
    <xsl:template match="strong | b">
        <strong>
            <xsl:apply-templates select="node()"/>
        </strong>
    </xsl:template>

    <xsl:template match="em | i">
        <em>
            <xsl:apply-templates select="node()"/>
        </em>
    </xsl:template>

    <xsl:template match="u">
        <u>
            <xsl:apply-templates select="node()"/>
        </u>
    </xsl:template>

    <xsl:template match="span">
        <span>
            <xsl:apply-templates select="node()"/>
        </span>
    </xsl:template>

    <!-- Links: preserve structure -->
    <xsl:template match="a">
        <a>
            <xsl:if test="@href">
                <xsl:attribute name="href" select="@href"/>
            </xsl:if>
            <xsl:apply-templates select="node()"/>
        </a>
    </xsl:template>

    <!-- Code and pre: preserve for technical content -->
    <xsl:template match="pre">
        <pre>
            <xsl:apply-templates select="node()"/>
        </pre>
    </xsl:template>

    <xsl:template match="code">
        <code>
            <xsl:apply-templates select="node()"/>
        </code>
    </xsl:template>

    <!-- Remove Docling-specific divs with special classes -->
    <xsl:template match="div[@class='formula']">
        <div class="formula">
            <xsl:apply-templates select="node()"/>
        </div>
    </xsl:template>

    <xsl:template match="div[@class='formula-not-decoded']">
        <!-- Skip undecoded formulas or preserve as text -->
        <p>
            <xsl:text>[Formula not decoded]</xsl:text>
        </p>
    </xsl:template>

    <xsl:template match="div[@class='page-break']">
        <!-- Remove page breaks -->
    </xsl:template>

    <xsl:template match="div[@class='key-value-region']">
        <!-- Transform key-value regions to simple divs -->
        <div>
            <xsl:apply-templates select="node()"/>
        </div>
    </xsl:template>

    <!-- Definition lists: preserve structure -->
    <xsl:template match="dl">
        <dl>
            <xsl:apply-templates select="node()"/>
        </dl>
    </xsl:template>

    <xsl:template match="dt">
        <dt>
            <xsl:apply-templates select="node()"/>
        </dt>
    </xsl:template>

    <xsl:template match="dd">
        <dd>
            <xsl:apply-templates select="node()"/>
        </dd>
    </xsl:template>

    <!-- Generic div: pass through content unless it's a special class -->
    <xsl:template match="div">
        <xsl:choose>
            <!-- Skip empty divs -->
            <xsl:when test="not(normalize-space(.))"/>
            <!-- Process content for generic divs -->
            <xsl:otherwise>
                <div>
                    <xsl:apply-templates select="node()"/>
                </div>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <!-- Text nodes: preserve -->
    <xsl:template match="text()">
        <xsl:value-of select="."/>
    </xsl:template>

    <!-- Ignore head and meta elements -->
    <xsl:template match="head | meta | title"/>

    <!-- Identity template for unmatched elements (fallback) -->
    <xsl:template match="*">
        <xsl:copy>
            <xsl:apply-templates select="@* | node()"/>
        </xsl:copy>
    </xsl:template>

</xsl:stylesheet>
