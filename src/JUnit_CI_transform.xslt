<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
<xsl:variable name="newline"><xsl:text></xsl:text></xsl:variable>
<xsl:template match="*">
<xsl:copy>
<xsl:apply-templates select="@*|node()"/>
</xsl:copy>
</xsl:template>
<xsl:template match="@*">
<xsl:copy-of select="."/>
</xsl:template>
<xsl:template match="failure/text()">
<xsl:value-of select="../@message"/>
<xsl:text>&#xa;</xsl:text>
<xsl:copy-of select="."/>
</xsl:template>
</xsl:stylesheet>