<?xml version="1.0" encoding="utf-8" ?>
<xsl:stylesheet
	version="1.0"
	xmlns="http://www.w3.org/1999/xhtml"
	xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
	xmlns:r="http://wisetechglobal.com/staticanalysis/2017/04/09/analysis.xsd"
	xmlns:svg="http://www.w3.org/2000/svg"
	xmlns:xlink="http://www.w3.org/1999/xlink"
	>
	<xsl:template match="/">
		<html>
			<head>
				<title>Analysis Results</title>
			</head>
			<body>
				<h1>Analysis Results</h1>
				<ul>
					<xsl:apply-templates select="r:report/r:solution[r:project/r:file]" />
				</ul>
			</body>
		</html>
	</xsl:template>

	<xsl:template match="r:solution">
		<li>
			<xsl:call-template name="filename">
				<xsl:with-param name="sub" select="@path" />
			</xsl:call-template>

			<ul>
				<xsl:apply-templates select="r:project[r:file]" />
			</ul>
		</li>
	</xsl:template>

	<xsl:template match="r:project">
		<li>
			<xsl:value-of select="@name" />

			<ul>
				<xsl:apply-templates select="r:file/r:diagnostic" />
			</ul>
		</li>
	</xsl:template>

	<xsl:template match="r:diagnostic">
		<li>
			<a>
				<xsl:attribute name="href">
					<xsl:text>vsnet:</xsl:text>
					<xsl:value-of select="ancestor::r:file/@path" />
					<xsl:text>#</xsl:text>
					<xsl:value-of select="substring-before(@from, ',')" />
				</xsl:attribute>
				<xsl:value-of select="@id" />
				<xsl:text>: </xsl:text>
				<xsl:call-template name="filename">
					<xsl:with-param name="sub" select="ancestor::r:file/@path" />
				</xsl:call-template>
				<xsl:text> (</xsl:text>
				<xsl:value-of select="@from" />
				<xsl:text>) - </xsl:text>
				<xsl:value-of select="@message" />
			</a>
		</li>
	</xsl:template>

	<xsl:template name="filename">
		<xsl:param name="value" />
		<xsl:param name="sub" select="substring-after($value, '\')" />
		<xsl:choose>
			<xsl:when test="$sub">
				<xsl:call-template name="filename">
					<xsl:with-param name="value" select="$sub" />
				</xsl:call-template>
			</xsl:when>
			<xsl:otherwise>
				<xsl:value-of select="$value" />
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>
</xsl:stylesheet>
