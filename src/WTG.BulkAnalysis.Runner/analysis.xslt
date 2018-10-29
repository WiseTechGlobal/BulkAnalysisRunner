<?xml version="1.0" encoding="utf-8" ?>
<xsl:stylesheet
	version="1.0"
	xmlns="http://www.w3.org/1999/xhtml"
	xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
	xmlns:r="http://wisetechglobal.com/staticanalysis/2017/04/09/analysis.xsd"
	xmlns:svg="http://www.w3.org/2000/svg"
	xmlns:xlink="http://www.w3.org/1999/xlink"
	>
	<xsl:key name="diagnosticsByID" match="/r:report/r:solution/r:project/r:file/r:diagnostic" use="@id" />

	<xsl:template match="/">
		<html>
			<head>
				<title>Analysis Results</title>
			</head>
			<body>
				<h1>Analysis Results</h1>

				<xsl:variable name="ruleIDs" select="/r:report/r:solution/r:project/r:file/r:diagnostic[generate-id(.) = generate-id(key('diagnosticsByID', @id))]/@id" />

				<xsl:value-of select="count(r:report/r:solution/r:project/r:file/r:diagnostic)" /> failures.<br />
				<xsl:value-of select="count(r:report/r:solution/r:project/r:file)" /> files.<br />

				<table>
					<thead>
						<tr>
							<th>Project</th>
							<xsl:for-each select="$ruleIDs">
								<th>
									<xsl:value-of select="." />
								</th>
							</xsl:for-each>
						</tr>
					</thead>
					<tbody>
						<xsl:for-each select="/r:report/r:solution/r:project[r:file]">
							<xsl:sort select="@name" />
							<xsl:variable name="project" select="current()" />
							<tr>
								<td>
									<a href="#{generate-id(.)}">
										<xsl:value-of select="@name" />
									</a>
								</td>

								<xsl:for-each select="$ruleIDs">
									<xsl:variable name="ruleID" select="." />
									<td>
										<xsl:value-of select="count($project/r:file/r:diagnostic[@id = $ruleID])" />
									</td>
								</xsl:for-each>
							</tr>
						</xsl:for-each>
					</tbody>
					<tfoot>
						<tr>
							<td />
							<xsl:for-each select="$ruleIDs">
								<xsl:variable name="ruleID" select="." />
								<td>
									<xsl:value-of select="count(/r:report/r:solution/r:project/r:file/r:diagnostic[@id = $ruleID])" />
								</td>
							</xsl:for-each>
						</tr>
					</tfoot>
				</table>

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
			<xsl:text> (</xsl:text>
			<xsl:value-of select="count(r:project/r:file/r:diagnostic)" />
			<xsl:text> failures)</xsl:text>

			<ul>
				<xsl:apply-templates select="r:project[r:file]" />
			</ul>
		</li>
	</xsl:template>

	<xsl:template match="r:project">
		<li>
			<a id="#{generate-id(.)}">
				<xsl:value-of select="@name" />
			</a>
			<xsl:text> (</xsl:text>
			<xsl:value-of select="count(r:file/r:diagnostic)" />
			<xsl:text> failures)</xsl:text>

			<ul>
				<xsl:apply-templates select="r:file/r:diagnostic" />
			</ul>
		</li>
	</xsl:template>

	<xsl:template match="r:diagnostic">
		<li>
			<a>
				<xsl:attribute name="href">
					<xsl:text>file://</xsl:text>
					<xsl:value-of select="ancestor::r:file/@path" />
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
