// Monaco Editor Integration for XSLT Editing

window.MonacoEditorInterop = {
    editor: null,
    dotNetRef: null,
    isSettingValueProgrammatically: false,

    /**
     * Initialize Monaco Editor for XSLT editing
     * @param {string} elementId - The ID of the container element
     * @param {string} initialValue - Initial content for the editor
     * @param {object} dotNetReference - Reference to Blazor component for callbacks
     * @param {string} theme - Editor theme ('vs-dark' or 'vs-light')
     */
    initialize: function (elementId, initialValue, dotNetReference, theme = 'vs-light') {
        this.dotNetRef = dotNetReference;

        const container = document.getElementById(elementId);
        if (!container) {
            console.error(`Container element '${elementId}' not found`);
            return false;
        }

        try {
            // Create Monaco Editor instance
            this.editor = monaco.editor.create(container, {
                value: initialValue || '',
                language: 'xml', // Use XML language for XSLT
                theme: theme,
                automaticLayout: true,
                minimap: {
                    enabled: true
                },
                scrollBeyondLastLine: false,
                fontSize: 13,
                lineHeight: 20,
                tabSize: 2,
                insertSpaces: true,
                wordWrap: 'on',
                lineNumbers: 'on',
                glyphMargin: true,
                folding: true,
                // XSLT-specific options
                autoIndent: 'full',
                formatOnPaste: true,
                formatOnType: true,
                bracketPairColorization: {
                    enabled: true
                },
                suggest: {
                    showKeywords: true,
                    showSnippets: true
                }
            });

            // Add XSLT-specific snippets and auto-completion
            this.registerXsltSnippets();

            // Handle content changes with debouncing
            let changeTimeout;
            this.editor.onDidChangeModelContent(() => {
                // Skip change event if we're setting value programmatically
                if (this.isSettingValueProgrammatically) {
                    console.log('Monaco: Skipping change event (programmatic setValue)');
                    return;
                }

                clearTimeout(changeTimeout);
                changeTimeout = setTimeout(() => {
                    if (this.dotNetRef) {
                        const content = this.editor.getValue();
                        this.dotNetRef.invokeMethodAsync('OnEditorContentChanged', content);
                    }
                }, 500); // 500ms debounce
            });

            // Add keyboard shortcuts
            this.addKeyboardShortcuts();

            console.log('Monaco Editor initialized successfully');
            return true;
        } catch (error) {
            console.error('Error initializing Monaco Editor:', error);
            return false;
        }
    },

    /**
     * Register XSLT-specific code snippets
     */
    registerXsltSnippets: function () {
        if (!monaco) return;

        monaco.languages.registerCompletionItemProvider('xml', {
            provideCompletionItems: (model, position) => {
                const suggestions = [
                    {
                        label: 'xsl:template',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: '<xsl:template match="${1:pattern}">\n\t$0\n</xsl:template>',
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'XSLT template with match pattern'
                    },
                    {
                        label: 'xsl:apply-templates',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: '<xsl:apply-templates select="${1:xpath}"/>',
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'Apply templates to selected nodes'
                    },
                    {
                        label: 'xsl:value-of',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: '<xsl:value-of select="${1:xpath}"/>',
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'Output value of XPath expression'
                    },
                    {
                        label: 'xsl:for-each',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: '<xsl:for-each select="${1:xpath}">\n\t$0\n</xsl:for-each>',
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'Loop over selected nodes'
                    },
                    {
                        label: 'xsl:if',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: '<xsl:if test="${1:condition}">\n\t$0\n</xsl:if>',
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'Conditional processing'
                    },
                    {
                        label: 'xsl:choose',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: '<xsl:choose>\n\t<xsl:when test="${1:condition}">\n\t\t$0\n\t</xsl:when>\n\t<xsl:otherwise>\n\t\t\n\t</xsl:otherwise>\n</xsl:choose>',
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'Multiple conditional branches'
                    },
                    {
                        label: 'xsl:variable',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: '<xsl:variable name="${1:varName}" select="${2:value}"/>',
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'Declare a variable'
                    },
                    {
                        label: 'xsl:copy-of',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: '<xsl:copy-of select="${1:xpath}"/>',
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'Copy nodes to output'
                    }
                ];

                return { suggestions };
            }
        });
    },

    /**
     * Add custom keyboard shortcuts
     */
    addKeyboardShortcuts: function () {
        if (!this.editor) return;

        // Ctrl+S: Save (will be handled by Blazor)
        this.editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS, () => {
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnSaveShortcut');
            }
        });

        // Ctrl+Enter: Transform (will be handled by Blazor)
        this.editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter, () => {
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnTransformShortcut');
            }
        });

        // F11: Toggle fullscreen (handled natively by Monaco)
    },

    /**
     * Get current editor content
     */
    getValue: function () {
        if (!this.editor) return '';
        return this.editor.getValue();
    },

    /**
     * Set editor content
     * @param {string} value - Content to set
     */
    setValue: function (value) {
        if (!this.editor) return;

        // Set flag to prevent change event from firing
        this.isSettingValueProgrammatically = true;
        this.editor.setValue(value || '');

        // Clear flag after a short delay (to ensure change event is skipped)
        setTimeout(() => {
            this.isSettingValueProgrammatically = false;
        }, 100);
    },

    /**
     * Update editor theme
     * @param {string} theme - Theme name ('vs-dark' or 'vs-light')
     */
    setTheme: function (theme) {
        if (!this.editor) return;
        monaco.editor.setTheme(theme);
    },

    /**
     * Focus the editor
     */
    focus: function () {
        if (!this.editor) return;
        this.editor.focus();
    },

    /**
     * Format the document
     */
    formatDocument: function () {
        if (!this.editor) return;
        this.editor.getAction('editor.action.formatDocument').run();
    },

    /**
     * Set error markers for validation errors
     * @param {Array} errors - Array of error objects with line, column, message
     */
    setErrors: function (errors) {
        if (!this.editor) return;

        const model = this.editor.getModel();
        if (!model) return;

        const markers = errors.map(error => ({
            severity: monaco.MarkerSeverity.Error,
            startLineNumber: error.line || 1,
            startColumn: error.column || 1,
            endLineNumber: error.line || 1,
            endColumn: error.column ? error.column + 10 : 1000,
            message: error.message || 'Unknown error'
        }));

        monaco.editor.setModelMarkers(model, 'xslt-validation', markers);
    },

    /**
     * Clear error markers
     */
    clearErrors: function () {
        if (!this.editor) return;
        const model = this.editor.getModel();
        if (!model) return;
        monaco.editor.setModelMarkers(model, 'xslt-validation', []);
    },

    /**
     * Resize editor (useful after container size changes)
     */
    resize: function () {
        if (!this.editor) return;
        this.editor.layout();
    },

    /**
     * Dispose editor and cleanup
     */
    dispose: function () {
        if (this.editor) {
            this.editor.dispose();
            this.editor = null;
        }
        this.dotNetRef = null;
    }
};

// Handle page unload
window.addEventListener('beforeunload', () => {
    if (window.MonacoEditorInterop) {
        window.MonacoEditorInterop.dispose();
    }
});

// File download utility
window.downloadFile = function (fileName, contentType, base64Content) {
    try {
        // Convert base64 to blob
        const binaryString = window.atob(base64Content);
        const bytes = new Uint8Array(binaryString.length);
        for (let i = 0; i < binaryString.length; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }
        const blob = new Blob([bytes], { type: contentType });

        // Create download link
        const link = document.createElement('a');
        link.href = window.URL.createObjectURL(blob);
        link.download = fileName;

        // Trigger download
        document.body.appendChild(link);
        link.click();

        // Cleanup
        document.body.removeChild(link);
        window.URL.revokeObjectURL(link.href);

        console.log(`Downloaded file: ${fileName}`);
    } catch (error) {
        console.error('Error downloading file:', error);
        throw error;
    }
};

// LocalStorage utilities for persisting UI preferences (not file selections)
window.developmentStorage = {

    saveSettings: function(useXslt3Service, normalizeHeaders, autoTransform) {
        try {
            localStorage.setItem('dev_useXslt3Service', useXslt3Service.toString());
            localStorage.setItem('dev_normalizeHeaders', normalizeHeaders.toString());
            localStorage.setItem('dev_autoTransform', autoTransform.toString());
            console.log(`Saved settings: xslt3=${useXslt3Service}, normalize=${normalizeHeaders}, auto=${autoTransform}`);
        } catch (error) {
            console.error('Error saving settings to localStorage:', error);
        }
    },

    loadSettings: function() {
        try {
            const useXslt3Service = localStorage.getItem('dev_useXslt3Service');
            const normalizeHeaders = localStorage.getItem('dev_normalizeHeaders');
            const autoTransform = localStorage.getItem('dev_autoTransform');

            const settings = {
                useXslt3Service: useXslt3Service === 'true',
                normalizeHeaders: normalizeHeaders !== 'false', // Default to true if not set
                autoTransform: autoTransform === 'true',
                hasStoredSettings: useXslt3Service !== null || normalizeHeaders !== null || autoTransform !== null
            };

            console.log(`Loaded settings: xslt3=${settings.useXslt3Service}, normalize=${settings.normalizeHeaders}, auto=${settings.autoTransform}`);
            return settings;
        } catch (error) {
            console.error('Error loading settings from localStorage:', error);
            return {
                useXslt3Service: true,
                normalizeHeaders: true,
                autoTransform: false,
                hasStoredSettings: false
            };
        }
    },

    clearSettings: function() {
        try {
            localStorage.removeItem('dev_useXslt3Service');
            localStorage.removeItem('dev_normalizeHeaders');
            localStorage.removeItem('dev_autoTransform');
            console.log('Cleared saved settings');
        } catch (error) {
            console.error('Error clearing settings from localStorage:', error);
        }
    }
};
