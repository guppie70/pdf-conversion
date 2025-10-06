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
    }
};

console.log('XML Formatter loaded successfully');
