// XML Formatter for XHTML Output
// Provides proper indentation for nested XML/XHTML elements

window.xmlFormatter = {
    /**
     * Format XML/XHTML string with proper indentation
     * @param {string} xml - Raw XML/XHTML content to format
     * @returns {string} Formatted XML with indentation
     */
    format: function(xml) {
        if (!xml || typeof xml !== 'string') {
            return xml || '';
        }

        try {
            const PADDING = '  '; // 2 spaces per indentation level

            // Add line breaks between tags
            const reg = /(>)(<)(\/*)/g;
            let formatted = xml.replace(reg, '$1\n$2$3');

            // Track indentation level
            let pad = 0;

            // Process each line and add appropriate indentation
            formatted = formatted.split('\n').map(line => {
                let indent = 0;

                // Self-closing tag or closing tag on same line as opening
                if (line.match(/.+<\/\w[^>]*>$/)) {
                    indent = 0;
                }
                // Closing tag
                else if (line.match(/^<\/\w/)) {
                    if (pad > 0) {
                        pad -= 1;
                    }
                }
                // Self-closing tag
                else if (line.match(/^<\w[^>]*\/>/)) {
                    indent = 0;
                }
                // Opening tag
                else if (line.match(/^<\w[^>]*[^\/]>.*$/)) {
                    indent = 1;
                }

                // Apply current padding
                const padding = PADDING.repeat(pad);
                pad += indent;

                return padding + line;
            }).join('\n');

            return formatted;
        } catch (error) {
            console.error('XML formatting error:', error);
            return xml; // Return original on error
        }
    },

    /**
     * Format the content of a specific element by ID
     * @param {string} elementId - ID of the element containing XML text
     */
    formatElement: function(elementId) {
        const element = document.getElementById(elementId);
        if (!element) {
            console.warn(`Element with ID '${elementId}' not found`);
            return;
        }

        try {
            const raw = element.textContent;
            const formatted = this.format(raw);
            element.textContent = formatted;
            console.log(`Formatted XML for element '${elementId}' (${formatted.split('\n').length} lines)`);
        } catch (error) {
            console.error('Error formatting element:', error);
        }
    },

    /**
     * Show raw (unformatted) content in element
     * @param {string} elementId - ID of the element
     * @param {string} content - Raw content to display
     */
    showRawElement: function(elementId, content) {
        const element = document.getElementById(elementId);
        if (!element) {
            console.warn(`Element with ID '${elementId}' not found`);
            return;
        }

        try {
            element.textContent = content || '';
            console.log(`Showing raw XML for element '${elementId}'`);
        } catch (error) {
            console.error('Error showing raw content:', error);
        }
    },

    /**
     * Get line and character count statistics
     * @param {string} xml - XML content to analyze
     * @returns {object} Statistics object
     */
    getStats: function(xml) {
        if (!xml) {
            return { lines: 0, chars: 0, elements: 0 };
        }

        const lines = xml.split('\n').length;
        const chars = xml.length;
        const elements = (xml.match(/<[^\/][^>]*>/g) || []).length;

        return { lines, chars, elements };
    },

    /**
     * Apply syntax highlighting to XML content
     * @param {string} xml - Raw XML content
     * @returns {string} HTML with syntax highlighting
     */
    highlightXml: function(xml) {
        if (!xml || typeof xml !== 'string') {
            return xml || '';
        }

        try {
            // Escape HTML entities first
            const escaped = xml
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;');

            // Apply syntax highlighting with span elements
            const highlighted = escaped
                // Comments
                .replace(/(&lt;!--[\s\S]*?--&gt;)/g, '<span class="xml-comment">$1</span>')
                // CDATA
                .replace(/(&lt;!\[CDATA\[[\s\S]*?\]\]&gt;)/g, '<span class="xml-cdata">$1</span>')
                // Processing instructions
                .replace(/(&lt;\?[\s\S]*?\?&gt;)/g, '<span class="xml-pi">$1</span>')
                // DOCTYPE
                .replace(/(&lt;!DOCTYPE[\s\S]*?&gt;)/g, '<span class="xml-doctype">$1</span>')
                // Tags with attributes
                .replace(/(&lt;\/?)(\w+(?::\w+)?)((?:\s+[\w:]+(?:\s*=\s*(?:"[^"]*"|'[^']*'))?)*\s*)(\/?)(&gt;)/g, function(match, openTag, tagName, attributes, selfClose, closeTag) {
                    let result = '<span class="xml-tag">' + openTag + '</span>';
                    result += '<span class="xml-tag-name">' + tagName + '</span>';

                    if (attributes) {
                        // Highlight attributes
                        result += attributes.replace(/([\w:]+)(\s*=\s*)("[^"]*"|'[^']*')/g,
                            '<span class="xml-attr-name">$1</span>$2<span class="xml-attr-value">$3</span>');
                    }

                    result += '<span class="xml-tag">' + selfClose + closeTag + '</span>';
                    return result;
                });

            return highlighted;
        } catch (error) {
            console.error('XML syntax highlighting error:', error);
            return xml; // Return original on error
        }
    },

    /**
     * Apply syntax highlighting to an element's content
     * NOTE: Not currently used. Monaco Editor provides syntax highlighting.
     * Kept for backwards compatibility or future use cases.
     *
     * @param {string} elementId - ID of the element containing XML text
     * @param {boolean} animate - Whether to animate the transition (default: false)
     */
    highlightElement: function(elementId, animate = false) {
        const element = document.getElementById(elementId);
        if (!element) {
            console.warn(`Element with ID '${elementId}' not found`);
            return;
        }

        try {
            if (animate) {
                console.log(`Adding 'updating' class to '${elementId}' - fading out`);
                // Add updating class to hide element
                element.classList.add('updating');

                // Force a reflow to ensure the opacity transition starts
                void element.offsetHeight;

                // Wait for fade-out to complete before applying highlighting
                setTimeout(() => {
                    const raw = element.textContent;
                    const formatted = this.format(raw);
                    const highlighted = this.highlightXml(formatted);
                    element.innerHTML = highlighted;

                    console.log(`Applied syntax highlighting to element '${elementId}' (animated: ${animate})`);

                    // Wait for next frame to fade back in
                    requestAnimationFrame(() => {
                        console.log(`Removing 'updating' class from '${elementId}' - fading in`);
                        element.classList.remove('updating');
                    });
                }, 20); // Wait 20ms for fade-out to be visible
            } else {
                const raw = element.textContent;
                const formatted = this.format(raw);
                const highlighted = this.highlightXml(formatted);
                element.innerHTML = highlighted;
                console.log(`Applied syntax highlighting to element '${elementId}' (animated: ${animate})`);
            }
        } catch (error) {
            console.error('Error highlighting element:', error);
            if (animate) {
                element.classList.remove('updating');
            }
        }
    },

    /**
     * Save scroll position of an element
     * @param {string} elementId - ID of the element
     * @returns {object} Scroll position {top, left}
     */
    saveScrollPosition: function(elementId) {
        const element = document.getElementById(elementId);
        if (!element) {
            return { top: 0, left: 0 };
        }
        return {
            top: element.scrollTop,
            left: element.scrollLeft
        };
    },

    /**
     * Restore scroll position of an element
     * @param {string} elementId - ID of the element
     * @param {object} position - Scroll position {top, left}
     */
    restoreScrollPosition: function(elementId, position) {
        const element = document.getElementById(elementId);
        if (!element || !position) {
            return;
        }
        element.scrollTop = position.top;
        element.scrollLeft = position.left;
        console.log(`Restored scroll position for '${elementId}': top=${position.top}, left=${position.left}`);
    }
};

console.log('XML Formatter with syntax highlighting loaded successfully');
