<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet exclude-result-prefixes="saxon-java xs aps" version="2.0" xmlns:saxon-java="java:java.lang.Math" xmlns:xs="http://www.w3.org/2001/XMLSchema"
	xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:aps="http://theapsgroup.com/xmlns">

	<xsl:output encoding="UTF-8" indent="no" method="xml"/>

	<xsl:param name="replace-lang">
		<xsl:text>lorem</xsl:text>
		<!-- options: lorem, chinese-simplified, chinese-traditional -->
	</xsl:param>

	<xsl:variable name="caps">ABCDEFGHIJKLMNOPQRSTUVWXYZ</xsl:variable>
	<xsl:variable name="lower">abcdefghijklmnopqrstuvwxyz</xsl:variable>
	
	<xsl:template match="/">
		<xsl:apply-templates mode="lorem"/>
	</xsl:template>

	<!-- copy no lorem replacements -->
	<xsl:template match="element()">
		<xsl:copy>
			<xsl:apply-templates select="@*,node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- copy -->
	<xsl:template match="element()" mode="lorem">
		<xsl:copy>
			<xsl:apply-templates select="@*,node()" mode="lorem"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="attribute() | comment() | processing-instruction()" mode="lorem">
		<xsl:copy/>
	</xsl:template>

	<!-- section -->
	<xsl:template match="text()" mode="lorem">
		<xsl:choose>
			<xsl:when test="normalize-space(.) = ''">
				<xsl:value-of select="."/>
			</xsl:when>
			<xsl:otherwise>
				<xsl:call-template name="aps:getRandomText">
					<xsl:with-param name="text" select="."/>
				</xsl:call-template>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>
	
	<xsl:template match="system" mode="lorem">
		<xsl:element name="{local-name()}">
			<xsl:apply-templates/>
		</xsl:element>
	</xsl:template>
	
	<xsl:template match="script[not(@src)]" mode="lorem">
		<xsl:element name="{local-name()}">
			<xsl:for-each select="@*">
				<xsl:copy-of select="."/>
			</xsl:for-each>
			<xsl:if test="not(@type)">
				<xsl:attribute name="type">text/javascript</xsl:attribute>
			</xsl:if>
			<xsl:text disable-output-escaping="yes">&lt;![CDATA[</xsl:text>
			<xsl:value-of select="." disable-output-escaping="yes"/>
			<xsl:text disable-output-escaping="yes">]]&gt;</xsl:text>
		</xsl:element>
	</xsl:template>
	
	<xsl:template match="meta[not(@name='viewport')]" mode="lorem">
		<xsl:element name="{local-name()}">
			<xsl:copy-of select="@*[not(local-name()='content')]"/>
			<xsl:attribute name="content">
				<xsl:call-template name="aps:getRandomText">
					<xsl:with-param name="text" select="@content"/>
				</xsl:call-template>
			</xsl:attribute>
		</xsl:element>
	</xsl:template>
	

	<xsl:template match="section/language" mode="lorem">
		<xsl:choose>
			<xsl:when test="starts-with($replace-lang,'chinese-')">
				<language>ZH</language>
			</xsl:when>
			<xsl:otherwise>
				<xsl:next-match/>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>

	<!-- graphs -->
	<xsl:template match="graph/*/data//sum/text()" mode="lorem">
		<xsl:variable name="max" select="aps:getGraphMax(ancestor::graph[1])"/>
		<xsl:value-of select="aps:getRandomInt($max)"/>
	</xsl:template>

	<xsl:template match="graph/*/data//@ratio" mode="lorem">
		<xsl:attribute name="{name()}">
			<xsl:value-of select="aps:getRandomInt(2) + 1"/>
		</xsl:attribute>
	</xsl:template>

	<xsl:template match="graph/*/data//label/text()" mode="lorem">
		<xsl:choose>
			<xsl:when test="aps:isYear(.)">
				<xsl:value-of select="aps:getRandomInt(10) + 2010"/>
			</xsl:when>
			<xsl:when test="aps:isShortYear(.)">
				<xsl:text>'</xsl:text>
				<xsl:value-of select="aps:getRandomInt(10) + 10"/>
			</xsl:when>
			<xsl:otherwise>
				<xsl:call-template name="aps:getRandomText">
					<xsl:with-param name="text" select="."/>
				</xsl:call-template>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>

	<xsl:template match="graph/*/@bar-max | graph/*/@line-max" mode="lorem">
		<xsl:attribute name="{name()}" select="aps:getGraphMax(ancestor::graph[1])"/>
	</xsl:template>

	<xsl:template match="graph/*/@bar-min | graph/*/@line-min" mode="lorem">
		<xsl:attribute name="{name()}">0</xsl:attribute>
	</xsl:template>

	<xsl:template match="graph/*/data//@value" mode="lorem">
		<xsl:attribute name="{name()}">
			<xsl:variable name="max" select="aps:getGraphMax(ancestor::graph[1])"/>
			<xsl:variable name="non-zero" select="if(ancestor::graph-bar-group or ancestor::graph-stack-group) then(1) else(0.5)"/>
			<xsl:choose>
				<xsl:when test="ancestor::graph-waterfall">
					<xsl:choose>
						<xsl:when test="parent::end | parent::start">
							<xsl:value-of select="$max div 2"/>
						</xsl:when>
						<xsl:otherwise>
							<xsl:variable name="value" select="if(aps:getRandomBoolean()) then(-2) else (2)"/>
							<xsl:value-of select="aps:getNonZero(aps:getRandomDouble($value,1),1)"/>
						</xsl:otherwise>
					</xsl:choose>
				</xsl:when>
				<xsl:when test="parent::point">
					<xsl:value-of select="aps:getNonZero(aps:getRandomInt($max - 2),0.5)"/>
				</xsl:when>
				<xsl:when test="parent::stack">
					<xsl:value-of select="aps:getNonZero(aps:getRandomInt($max div count(ancestor::bar/stack)),$non-zero)"/>
				</xsl:when>
				<xsl:otherwise>
					<xsl:value-of select="aps:getNonZero(aps:getRandomInt($max - 2),$non-zero)"/>
				</xsl:otherwise>
			</xsl:choose>
		</xsl:attribute>
	</xsl:template>

	<!-- table -->
	<xsl:template match="tbody//td/p/text()" mode="lorem">
		<xsl:choose>
			<xsl:when test="ancestor::td[1]/@format = 'rate-indicator'">
				<xsl:value-of select="aps:getRandomInt(if(aps:getRandomBoolean()) then(-5) else(5))"/>
				<xsl:text>%</xsl:text>
			</xsl:when>
			<xsl:when test="aps:isYear(.)">
				<xsl:value-of select="aps:getRandomInt(10) + 2010"/>
			</xsl:when>
			<xsl:when test="aps:isShortYear(.)">
				<xsl:text>'</xsl:text>
				<xsl:value-of select="aps:getRandomInt(10) + 10"/>
			</xsl:when>
			<xsl:when test="aps:isNumber(.)">
				<xsl:variable name="number">
					<xsl:choose>
						<xsl:when test="contains(.,'.')">
							<xsl:variable name="decimals" select="string-length(substring-after(.,'.'))"/>
							<xsl:value-of select="aps:getNonZero(aps:getRandomDouble(aps:getNumber(.),$decimals),1)"/>
						</xsl:when>
						<xsl:otherwise>
							<xsl:value-of select="aps:getNonZero(aps:getRandomInt(aps:getNumber(.)),1)"/>
						</xsl:otherwise>
					</xsl:choose>
				</xsl:variable>
				<xsl:variable name="random-abs" select="if(aps:getRandomInt(10) > 7) then($number * -1) else($number)"/>
				<xsl:variable name="rounded" select="round-half-to-even($random-abs,2)"/>
				<xsl:value-of select="aps:getFormattedNumber($rounded)"/>
			</xsl:when>
			<xsl:otherwise>
				<xsl:call-template name="aps:getRandomText">
					<xsl:with-param name="text" select="."/>
				</xsl:call-template>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>
	
	<xsl:template match="div[@data-charttype]//td/text()" mode="lorem">
		<xsl:choose>
			<xsl:when test="ancestor::td[1]/@format = 'rate-indicator'">
				<xsl:value-of select="aps:getRandomInt(if(aps:getRandomBoolean()) then(-5) else(5))"/>
				<xsl:text>%</xsl:text>
			</xsl:when>
			<xsl:when test="aps:isYear(.)">
				<xsl:value-of select="aps:getRandomInt(10) + 2010"/>
			</xsl:when>
			<xsl:when test="aps:isShortYear(.)">
				<xsl:text>'</xsl:text>
				<xsl:value-of select="aps:getRandomInt(10) + 10"/>
			</xsl:when>
			<xsl:when test="aps:isNumber(.)">
				<xsl:variable name="number">
					<xsl:choose>
						<xsl:when test="contains(.,'.')">
							<xsl:variable name="decimals" select="string-length(substring-after(.,'.'))"/>
							<xsl:value-of select="aps:getNonZero(aps:getRandomDouble(aps:getNumber(.),$decimals),1)"/>
						</xsl:when>
						<xsl:otherwise>
							<xsl:value-of select="aps:getNonZero(aps:getRandomInt(aps:getNumber(.)),1)"/>
						</xsl:otherwise>
					</xsl:choose>
				</xsl:variable>
				<xsl:variable name="random-abs" select="if(aps:getRandomInt(10) > 7) then($number * -1) else($number)"/>
				<xsl:variable name="rounded" select="round-half-to-even($random-abs,2)"/>
				<xsl:value-of select="aps:getFormattedNumber($rounded)"/>
			</xsl:when>
			<xsl:otherwise>
				<xsl:call-template name="aps:getRandomText">
					<xsl:with-param name="text" select="."/>
				</xsl:call-template>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>

	<!-- website data -->
	<xsl:template match="/data/item/value/text()" mode="lorem">
		<xsl:value-of select="aps:getRandomInt(10)"/>
	</xsl:template>

	<!-- don't replace -->
	<xsl:template match="/data/item/_calyear/text()" mode="lorem">
		<xsl:value-of select="."/>
	</xsl:template>

	<xsl:template match="section/message/text() | section/language/text()" mode="lorem">
		<xsl:value-of select="."/>
	</xsl:template>

	<xsl:template match="section/metadata//text()" mode="lorem">
		<xsl:value-of select="."/>
	</xsl:template>
	
	<xsl:template match="footNote/prefix/text()" mode="lorem">
		<xsl:value-of select="."/>
	</xsl:template>

	<!-- functions -->
	<xsl:function name="aps:getGraphMax" as="xs:integer">
		<xsl:param name="graph" as="element(graph)"/>
		<xsl:choose>
			<xsl:when test="$graph/graph-waterfall">10</xsl:when>
			<xsl:when test="$graph/graph-stack">8</xsl:when>
			<xsl:when test="$graph/graph-stack-group">8</xsl:when>
			<xsl:otherwise>8</xsl:otherwise>
		</xsl:choose>
	</xsl:function>

	<xsl:function name="aps:getNonZero" as="xs:double">
		<xsl:param name="value" as="xs:double"/>
		<xsl:param name="fallback" as="xs:double"/>
		<xsl:value-of select="if($value = 0) then($fallback) else($value)"/>
	</xsl:function>

	<xsl:function name="aps:isYear" as="xs:boolean">
		<xsl:param name="year" as="xs:string?"/>
		<xsl:value-of select="string-length(normalize-space($year)) = 4 and aps:isNumber($year) and not(contains($year,'.'))"/>
	</xsl:function>

	<xsl:function name="aps:isShortYear" as="xs:boolean">
		<xsl:param name="year" as="xs:string?"/>
		<xsl:variable name="apos">'</xsl:variable>
		<xsl:value-of select="string-length(normalize-space($year)) = 3 and starts-with($year,$apos) and aps:isNumber(substring($year,2))"/>
	</xsl:function>

	<xsl:function name="aps:isNumber" as="xs:boolean">
		<xsl:param name="string" as="xs:string?"/>
		<xsl:value-of select="string(number(replace($string,',',''))) != 'NaN'"/>
	</xsl:function>

	<xsl:function name="aps:getNumber" as="xs:double">
		<xsl:param name="string" as="xs:string?"/>
		<xsl:value-of select="number(replace($string,',',''))"/>
	</xsl:function>

	<xsl:function name="aps:getFormattedNumber" as="xs:string">
		<xsl:param name="number" as="xs:double"/>
		<xsl:value-of select="format-number($number,'##,##0.##')"/>
	</xsl:function>

	<xsl:function name="aps:getRandomBoolean" as="xs:boolean">
		<xsl:value-of select="saxon-java:random() >= 0.5"/>
	</xsl:function>

	<xsl:function name="aps:getRandomInt" as="xs:double">
		<xsl:param name="max" as="xs:double"/>
		<xsl:value-of select="round(($max) * saxon-java:random())"/>
	</xsl:function>

	<xsl:function name="aps:getRandomDouble" as="xs:double">
		<xsl:param name="max" as="xs:double"/>
		<xsl:param name="decimals" as="xs:integer"/>
		<xsl:value-of select="round-half-to-even(($max) * saxon-java:random(),$decimals)"/>
	</xsl:function>

	<xsl:template name="aps:getRandomText">
		<xsl:param name="text">none</xsl:param>
		<!-- get text element-->
		<xsl:if test="count($text-container/lang[@id = $replace-lang]) = 0">
			<xsl:message terminate="yes">
				<xsl:text>Unable to find dummy text for language '</xsl:text>
				<xsl:value-of select="$replace-lang"/>
				<xsl:text>', use one of </xsl:text>
				<xsl:for-each select="$text-container/lang">
					<xsl:text>[</xsl:text>
					<xsl:value-of select="@id"/>
					<xsl:text>] </xsl:text>
				</xsl:for-each>
			</xsl:message>
		</xsl:if>
		<xsl:variable name="lang-element" select="$text-container/lang[@id = $replace-lang]"/>
		<!-- get random paragraph -->
		<xsl:variable name="p-count" select="count($lang-element/p) - 1"/>
		<xsl:variable name="p-random" select="round($p-count * saxon-java:random())"/>
		<xsl:variable name="p" select="$lang-element/p[$p-random + 1]"/>
		<!-- get replacement text -->
		<xsl:variable name="text-length" select="string-length($text) * $lang-element/@ratio"/>
		<xsl:variable name="text-length-p" select="string-length($p/text())"/>
		<!-- punctuation -->
		<xsl:variable name="text-normal" select="normalize-space($text)"/>
		<xsl:variable name="punctuation">.,!?;:'"%()+=-*@#$€&#x2212;&amp;</xsl:variable>
		<!-- add space before if present in original -->
		<xsl:if test="starts-with($text,' ')">
			<xsl:text xml:space="preserve"> </xsl:text>
		</xsl:if>
		<xsl:choose>
			<xsl:when test="string-length($text-normal) = 1 and contains($punctuation,$text-normal)">
				<!-- preserve original text in some cases -->
				<xsl:value-of select="$text"/>
			</xsl:when>
			<xsl:when test="$text-length >= $text-length-p">
				<!-- if original text is longer than replacement text, just render the replacement text -->
				<xsl:value-of select="$p/text()"/>
			</xsl:when>
			<xsl:otherwise>
				<!-- get a random piece of the replacement text with the same length as the orignal text -->
				<xsl:variable name="text-start" select="$text-length-p - $text-length"/>
				<xsl:variable name="text-random-start" select="ceiling(saxon-java:random() * $text-start)"/>
				<xsl:variable name="text-lorem" select="substring($p/text(),$text-random-start,$text-length)"/>
				<xsl:variable name="text-lorem-normal" select="normalize-space($text-lorem)"/>
				<xsl:choose>
					<xsl:when test="contains($text,' ')">
						<!-- capitalize text if it is a sentence -->
						<xsl:variable name="text-capitalized"
							select="concat(translate(substring($text-lorem-normal,1,1),$lower,$caps),substring($text-lorem-normal,2,string-length($text-lorem-normal)))"/>
						<!-- render lorem text -->
						<xsl:value-of select="$text-capitalized"/>
					</xsl:when>
					<xsl:otherwise>
						<!-- otherwise, just display the word -->
						<xsl:value-of select="$text-lorem-normal"/>
					</xsl:otherwise>
				</xsl:choose>
			</xsl:otherwise>
		</xsl:choose>
		<!-- add space after if present in original -->
		<xsl:if test="ends-with($text,' ')">
			<xsl:text xml:space="preserve"> </xsl:text>
		</xsl:if>
	</xsl:template>

	<!-- samples -->
	<xsl:variable as="element(text)" name="text-container">
		<text>
			<lang id="lorem" ratio="1">
				<p>lorem ipsum dolor sit amet consectetur adipiscing elit mauris laoreet vehicula est eu ullamcorper tellus egestas sit amet suspendisse potenti etiam eu diam turpis a hendrerit sem proin vulputate arcu lobortis lacus sagittis varius in hac habitasse platea dictumst nam lacinia rhoncus auctor cras ut convallis odio fusce metus purus egestas id lobortis et imperdiet et velit quisque placerat vestibulum libero at tempor ligula pulvinar non morbi elit turpis vestibulum eu iaculis non tempor vitae turpis vestibulum varius libero ultrices turpis hendrerit gravida nam eu nibh ante nunc porttitor lacus nisi aliquam erat volutpat aliquam at blandit risus maecenas ac sodales libero nunc id est metus et tristique orci phasellus porta pretium arcu vel pharetra</p>
				<p>duis eleifend scelerisque ante in vestibulum nullam ac dui in odio posuere consequat nec quis odio morbi luctus augue a diam egestas non placerat urna luctus aenean tincidunt mollis libero at ultricies elit pulvinar blandit pellentesque habitant morbi tristique senectus et netus et malesuada fames ac turpis egestas phasellus auctor est eget sapien lacinia suscipit non id ante vestibulum massa nunc rhoncus sed molestie sed sodales vel magna sed consequat nisi in faucibus ullamcorper dui turpis pellentesque mi sit amet mattis neque metus in dolor donec ante nisl adipiscing id bibendum quis laoreet eu augue integer vestibulum massa et vehicula faucibus turpis libero tristique lorem ac tincidunt risus nunc id nunc</p>
				<p>donec euismod vulputate dolor porttitor suscipit vivamus auctor elit eget dui porta tempor pellentesque imperdiet velit molestie magna pulvinar ac imperdiet massa egestas curabitur semper aliquam augue iaculis vestibulum praesent mauris lorem tincidunt eu iaculis pharetra tristique ut turpis vestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae etiam adipiscing sagittis augue vel dapibus vivamus vel orci id erat facilisis consectetur mauris tristique quam vel enim imperdiet commodo etiam eget leo a dui auctor sodales sit amet vitae tortor phasellus rutrum leo a dui adipiscing sit amet hendrerit ante dignissim etiam in est sed sapien egestas interdum quisque hendrerit aliquet felis in laoreet felis mattis quis vestibulum sem massa tempus non sollicitudin in tincidunt in urna cras scelerisque sapien et urna placerat condimentum</p>
				<p>nulla diam ante luctus eu elementum id rhoncus nec mauris praesent sollicitudin augue vitae quam malesuada in euismod diam vulputate etiam quis magna nisi nunc ut neque nisi sed vel enim urna suspendisse et justo a nunc congue elementum sed blandit porttitor luctus curabitur varius tempus nisi quis egestas enim commodo non integer porttitor dignissim lacus vel volutpat suspendisse imperdiet tellus arcu in hac habitasse platea dictumst suspendisse suscipit lacus sed mattis faucibus nulla dolor lobortis augue sed convallis lectus mauris consequat tellus</p>
				<p>aliquam erat volutpat integer sit amet libero est sed dapibus magna suspendisse laoreet eros eu aliquet eleifend odio odio vestibulum sapien nec pulvinar neque magna quis nunc cras posuere eros quis justo vestibulum sollicitudin praesent a lectus quam etiam erat lectus molestie sed ultricies a malesuada ut odio mauris euismod metus in orci pharetra consectetur aenean at libero sit amet felis dignissim aliquet quisque convallis tristique arcu nec gravida ipsum fringilla consectetur aenean et leo vestibulum risus rhoncus vulputate fermentum quis velit </p>
				<p>maecenas pellentesque ligula sit amet vulputate ornare lectus leo ullamcorper nisl vitae sollicitudin nunc augue nec enim nunc faucibus eros eget faucibus volutpat nunc nibh condimentum metus eu malesuada nibh odio at diam donec varius ornare ante at vulputate donec eleifend neque eu diam venenatis varius aliquam sem est suscipit id adipiscing sed dapibus vel urna aenean volutpat dignissim malesuada phasellus feugiat auctor porta ut placerat augue et lectus euismod nec hendrerit tortor congue nulla cursus iaculis dolor eu fringilla vivamus consequat sem malesuada mi rutrum fermentum nec non sapien mauris vel neque erat phasellus magna quam laoreet at ultrices vitae tristique quis nulla integer ultricies aliquet dolor ac lacinia donec ullamcorper gravida posuere integer quam diam dictum sit amet venenatis ac pretium non massa aenean nibh ligula adipiscing id posuere blandit ultricies sed diam ut dolor nunc cursus eget interdum at lacinia eu enim cras sit amet ornare lacus</p>
				<p>etiam viverra consequat libero eget consectetur neque pretium ut suspendisse cursus posuere diam at viverra phasellus adipiscing vehicula gravida mauris erat tortor sollicitudin et ullamcorper ullamcorper accumsan nec justo fusce eu felis non turpis blandit pretium nec vel eros sed sed commodo ipsum ut enim velit ultricies eget condimentum a euismod vitae risus vestibulum elementum luctus turpis at congue nulla dapibus pulvinar dolor sed tristique aenean at est a elit dignissim pharetra pellentesque cursus sapien id quam tempor fermentum nulla placerat libero nec risus cursus vel tempus turpis ornare sed auctor magna quis diam luctus adipiscing cras bibendum nibh ac semper aliquam nisi dui tincidunt dolor vel scelerisque nunc nulla in lorem aliquam sit amet lacus tortor sit amet mollis nunc praesent sagittis ante ut arcu sollicitudin in congue justo tempor pellentesque habitant morbi tristique senectus et netus et malesuada fames ac turpis egestas</p>
				<p>etiam venenatis lectus fringilla quam pretium faucibus morbi molestie lobortis nisi in auctor nunc sed est a nunc facilisis posuere in hac habitasse platea dictumst proin eget ipsum magna sit amet ullamcorper libero nam sit amet felis ac nibh gravida pretium proin tellus mi tempor et dignissim sit amet dapibus vitae mi cras tincidunt lacinia orci ut vehicula tortor dapibus et aliquam erat volutpat aliquam condimentum hendrerit commodo vestibulum vestibulum augue vel nulla faucibus egestas phasellus nec justo vitae dolor mattis cursus</p>
				<p>quisque sit amet nunc purus id pellentesque tortor quisque justo neque semper sed suscipit sed porttitor at elit donec leo lacus rhoncus sed viverra sed vulputate eget arcu suspendisse varius scelerisque pharetra aenean eget odio risus etiam sit amet quam felis nec porttitor leo morbi lorem dolor adipiscing a elementum nec iaculis quis eros quisque at nunc dui ut consectetur accumsan vestibulum integer tempus placerat lobortis curabitur lacinia ornare porttitor aenean pharetra elementum ante et interdum duis id eros sit amet justo tristique gravida sit amet quis velit</p>
				<p>sed non mauris vestibulum arcu tincidunt consectetur non ut magna donec quis urna risus in hac habitasse platea dictumst in mollis lacus scelerisque diam rhoncus fringilla nam molestie ornare cursus quisque ultrices augue sed dictum eleifend tellus quam mollis neque vitae aliquam sapien arcu non nunc quisque sed congue nisi suspendisse sodales tellus eu feugiat volutpat purus ante facilisis ante vel lacinia orci metus lobortis mauris phasellus lacinia posuere tempor proin viverra nibh quis dictum egestas risus orci commodo justo sit amet semper nibh sapien ut mauris suspendisse potenti vestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae mauris at arcu ut dui aliquam pretium a eu lorem nullam adipiscing tortor quis diam rutrum vel ultrices arcu consectetur sed blandit auctor nibh nec vestibulum est iaculis vel cras tortor lorem venenatis ut lobortis vitae malesuada eget risus fusce ut sem ac quam lacinia faucibus aliquam lectus elit tincidunt quis dignissim non adipiscing vel ante</p>
			</lang>
			<lang id="chinese-traditional" ratio="0.38">
				<p>僅僅是虛擬的純文字和排版印刷行業。 一直是行業的標準假人文本自從 16世紀，當一個未知的打印機採取了廚房的類型和炒它做一個模式標本的書。它不僅延續了下來五世紀，但也躍升為電子排版，其餘基本保持不變。這是20世紀 60年代普及與釋放張含 Letraset通道，以及最近與桌面出版軟件，如奧爾德斯 PageMaker中包括版本。</p>
				<p>t是一個歷史悠久的事實，讀者會分心的可讀內容的網頁時，看其佈局。這一點用是，它更具有或多或少的正態分佈的英文字母，而不是用'內容在這裡，這裡的內容'，使它看起來像讀英語。許多桌面排版軟體，網頁編輯器現在使用作為其默認示範文本，並搜索''會發現很多網站仍處於起步階段。各種版本有經過多年的發展，有時意外，有時是故意（注幽默之類）。</p>
				<p>與普遍看法相反，不是簡單隨機文本。它起源於一塊古典拉丁文學從公元前45年，使超過 2000歲。理查德麥克林托克，拉丁語教授漢普登，悉尼大學在弗吉尼亞州，抬頭看一個較模糊的拉丁詞，consectetur，從 通道，將通過引用這個詞的古典文學，發現了undoubtable來源。 來自第1.10.32和1.10.33“去Finibus Bonorum等Malorum”（極端的善和惡）西塞羅，寫在公元前45年。這本書是傷寒論理論倫理學，非常流行的文藝復興時期。第一行的“悲坐阿梅特..”，來自行第1.10.32。</p>
				<p>還有很多變化的段落的可用，但多數已出現某種形式的改變，通過注入幽默，或隨機的話不顯得稍微可信。如果你要使用一個通道的，你需要確保沒有什麼尷尬的隱藏在中間的文本。所有的發電機在互聯網上傾向於在必要時重複預定塊，使這首在互聯網上真正的發電機。它使用拉丁語字典超過 200字，再加上少數的句子結構模型，生成看起來是合理的。 生成的，因此始終擺脫重複，注入幽默，還是非等特徵詞</p>
				<p>但我必須向你解釋這一切是如何錯誤的想法，快樂的譴責，並讚揚疼痛出生，我會給你一個完整的系統帳戶，並闡述了實際教義的偉大探險家的道理，主建築商的人幸福。沒有人拒絕，不喜歡，或避免快感本身，因為它是快樂，而是因為那些誰不知道如何去追求快樂的後果，理性地面對非常痛苦的。再次是有沒有人也沒有誰愛或追求或希望獲得自身的痛苦，因為它是痛苦的，但因為偶爾的情況下發生的辛勞和痛苦可以促使他一些非常高興。舉一個小例子，我們不斷進行艱苦的體育鍛煉，除了從中得到一些好處？但是，誰擁有任何權利，以挑剔的人誰選擇享受樂趣，沒有惱人的後果，或一個誰避免了由此產生的痛苦，不產生快感？</p>
				<p>另一方面，我們譴責和義憤和不喜歡的人誰是如此引誘，情緒低落的魅力快樂的時刻，因此所蒙蔽的慾望，使他們無法預見的痛苦和麻煩也必然隨之而來;和平等的責任誰不屬於那些在他們的職責因軟弱的意志，這是相同的話說，通過從辛勞和痛苦萎縮。這些案件是非常簡單，易於區分。在一個自由的時刻，我們的權力是不受約束的選擇，當沒有什麼能阻止我們能夠做我們最喜歡的，老王賣瓜，是值得歡迎的，各種痛苦避免。但在某些情況下並由於索賠責任或義務的企業，將經常發生的樂趣，必須接受批判和煩惱。聰明的人，因此在這些問題上一貫主張這一原則的選擇：他拒絕的樂趣，以取得其他更大的樂趣，否則他忍受的痛苦，避免嚴重的痛苦。</p>
				<p>在出版和平面設計，是佔位符文本（填充文本）通常用來演示圖形元素的文件或視覺表現，如字體，排版和佈局。該 文本通常是第一個拉丁文字西塞羅文字修改，添加和刪除，使人們荒謬的意義和不正確的拉丁語。一個接近英文翻譯的話 可能是“疼痛本身”（dolorem =痛苦，悲傷，痛苦，痛苦; ipsum =本身）。</p>
				<p>即使使用“”常常喚起好奇心，因為它像古典拉丁語，它的目的不是具有意義。其中文本在文件中可見，人們往往把重點放在文本的內容，而不是在整體表現，因此出版商使用時顯示的字體或設計元素和頁面佈局，以便直接將焦點轉移到出版物的風格，而不是所指的文本。</p>
			</lang>
			<lang id="chinese-simplified" ratio="0.45">
				<p>是简单的印刷和排版行业的虚拟文本。 一直自从16世纪，当一个未知的打印机了厨房的类型和炒它的模式标本作书业界的标准假人文本。它存活到电子排版，不仅五个世纪，而且飞跃，其余基本保持不变。这是20世纪60年代普及与包含的Letraset代表的释放，更喜欢奥尔德斯PageMaker中包括版本的桌面出版软件最近。</p>
				<p>与普遍看法相反，不是简单随机文本。它在拉丁美洲的古典文学作品，从公元前45年的根，使超过2000岁了。理查德麦克林托克，在汉普顿，弗吉尼亚大学悉尼拉丁文教授，抬起头来的更隐蔽的拉丁词汇之一，consectetur，从通道，并经历了古典文学中列举的话，发现undoubtable来源。 来自第1.10.32和“德等Malorum规定”（善和极端恶）西塞罗在公元前45年写的，1.10.33。这本书是一个关于道德的理论，在文艺复兴时期非常流行的论文。 的第一线，“悲坐阿梅特..”，从第1.10.32线来。</p>
				<p>这是一个历史悠久的事实，读者将通过一个页面的可读内容分散在其布局时看。的使用一点是，它更具有或多或少的正态分布的英文字母，而不是用'内容在这里，这里的内容'，使它看起来像读英语。许多桌面排版软体，网页编辑器现在使用它们的默认示范文本，并为'的搜索将仍处于萌芽阶段发现许多网站。各种版本有经过多年的发展，有时意外，有时是故意（注幽默之类）。</p>
				<p>有可用的段落许多变化，但大多数都遭受了某种形式的改造，通过注入幽默，或随机的话不显得稍微可信。如果你要使用的的推移，你需要确保没有什么尴尬的文本中隐藏的。所有在互联网上发电机往往需要重复预定块，使这首在互联网上真正的发电机。它使用了超过200个拉丁词与句子结构的模型少数相结合，产生字典看起来是合理的。 生成的，因此总是从重复，注入幽默，或不自由等特征词</p>
				<p>的标准块使用，因为16世纪是低于那些有兴趣转载。第1.10.32和“德等Malorum”1.10.33西塞罗也转载了他们的确切原来的形式，从1914年由H.拉克姆英文版本翻译陪同。</p>
				<p>但我必须向你解释这一切是如何快乐的谴责，并赞扬疼痛出生，我会给你一个完整的系统帐户，并阐述了真理的，是总的人类伟大的探险家的实际建设者教诲误区幸福。没有人拒绝，不喜欢，或避免快感本身，因为它是快乐，而是因为那些谁不知道如何去追求快乐的后果，理性地面对非常痛苦的。再次是有没有人也没有谁爱或追求或希望获得自身的痛苦，因为它是痛苦的，但因为偶尔的情况下发生的辛劳和痛苦可以促使他一些非常高兴。举一个小例子，我们不断进行艰苦的体育锻炼，除了从中获得一些好处？但是，谁拥有任何权利找到一个男人谁选择享受乐趣，没有恼人的后果，或一个谁避免了由此产生的痛苦，不产生快感的错吗？</p>
				<p>另一方面，我们谴责和义愤，不喜欢谁是如此引诱，并由如此被欲望蒙蔽的时刻，快乐的魅力，他们无法预见的痛苦和麻烦也必然随之而来士气低落的男人，和平等的责任谁不属于那些在他们的职责，通过意志的弱点，这是因为通过从辛劳和痛苦收缩说相同。这些案件是非常简单，易于区分。在一个自由的时刻，我们选择的权力是不受约束的时候没有什么能阻止我们能够做我们最喜欢的，老王卖瓜，是值得欢迎的，各种痛苦避免。但在某些情况下，又由于对索赔或营业税的义务，将经常发生的乐趣，必须接受批判和烦恼。因此，聪明的人总是持有这种选择这些原则问题：他拒绝的乐趣，以争取其他大乐趣，否则，他忍受的痛苦，避免严重的痛苦。</p>
			</lang>
		</text>
	</xsl:variable>
</xsl:stylesheet>
