// Monaco XML Editor Integration for Source XML Editing
window.MonacoXmlEditorInterop = {
    editor: null,
    dotNetRef: null,
    isSettingValueProgrammatically: false,

    /**
     * Initialize Monaco Editor for XML editing
     */
    initialize: function (elementId, initialValue, dotNetReference, theme = 'vs-light') {
        console.log('MonacoXmlEditorInterop.initialize called:', {
            elementId,
            initialValueLength: initialValue?.length ?? 0,
            dotNetReferenceNull: !dotNetReference,
            theme,
            editorAlreadyExists: !!this.editor
        });

        this.dotNetRef = dotNetReference;

        const container = document.getElementById(elementId);
        if (!container) {
            console.error(`MonacoXmlEditor: Container element '${elementId}' not found`);
            return false;
        }

        console.log('MonacoXmlEditor: Container found, dimensions:', {
            width: container.offsetWidth,
            height: container.offsetHeight,
            display: window.getComputedStyle(container).display,
            visibility: window.getComputedStyle(container).visibility
        });

        try {
            // Dispose existing editor if it exists
            if (this.editor) {
                console.log('MonacoXmlEditor: Disposing existing editor before re-initialization');
                this.editor.dispose();
                this.editor = null;
            }

            // Create Monaco Editor instance
            console.log('MonacoXmlEditor: Creating editor with options...');
            this.editor = monaco.editor.create(container, {
                value: initialValue || '',
                language: 'xml',
                theme: theme,
                automaticLayout: true,
                minimap: { enabled: true },
                scrollBeyondLastLine: false,
                fontSize: 13,
                lineHeight: 20,
                tabSize: 2,
                insertSpaces: true,
                wordWrap: 'on',
                lineNumbers: 'on',
                glyphMargin: true,
                folding: true,
                autoIndent: 'full',
                formatOnPaste: true,
                formatOnType: true,
                bracketPairColorization: { enabled: true }
            });

            console.log('MonacoXmlEditor: Editor instance created, setting up event handlers...');

            // Handle content changes with debouncing
            let changeTimeout;
            this.editor.onDidChangeModelContent(() => {
                // Skip change event if we're setting value programmatically
                if (this.isSettingValueProgrammatically) {
                    console.log('MonacoXmlEditor: Skipping change event (programmatic setValue)');
                    return;
                }

                console.log('MonacoXmlEditor: Content changed, scheduling callback (debounced 500ms)');
                clearTimeout(changeTimeout);
                changeTimeout = setTimeout(() => {
                    if (this.dotNetRef) {
                        const content = this.editor.getValue();
                        console.log('MonacoXmlEditor: Invoking OnXmlContentChanged callback (content length: ' + content.length + ')');
                        this.dotNetRef.invokeMethodAsync('OnXmlContentChanged', content);
                    }
                }, 500); // 500ms debounce
            });

            // Add keyboard shortcuts
            this.addKeyboardShortcuts();

            console.log('MonacoXmlEditor: Initialization complete - SUCCESS');
            return true;
        } catch (error) {
            console.error('MonacoXmlEditor: Error initializing Monaco XML Editor:', error);
            return false;
        }
    },

    /**
     * Add custom keyboard shortcuts
     */
    addKeyboardShortcuts: function () {
        if (!this.editor) return;

        // Ctrl+S: Save
        this.editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS, () => {
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnSaveXmlShortcut');
            }
        });
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
     */
    setValue: function (value) {
        console.log('MonacoXmlEditorInterop.setValue called:', {
            valueLength: value?.length ?? 0,
            editorExists: !!this.editor
        });

        if (!this.editor) {
            console.error('MonacoXmlEditor: Cannot setValue - editor not initialized');
            return;
        }

        // Set flag to prevent change event from firing
        this.isSettingValueProgrammatically = true;
        this.editor.setValue(value || '');
        console.log('MonacoXmlEditor: Editor value set successfully');

        // Clear flag after a short delay
        setTimeout(() => {
            this.isSettingValueProgrammatically = false;
            console.log('MonacoXmlEditor: isSettingValueProgrammatically flag cleared');
        }, 100);
    },

    /**
     * Update editor theme
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
     * Resize editor
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
    if (window.MonacoXmlEditorInterop) {
        window.MonacoXmlEditorInterop.dispose();
    }
});
