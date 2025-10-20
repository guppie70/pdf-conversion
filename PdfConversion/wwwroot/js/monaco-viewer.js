/**
 * Monaco Viewer Module
 *
 * Reusable JavaScript module for managing multiple Monaco Editor instances
 * in read-only viewer mode. Designed for displaying XML content without editing capabilities.
 *
 * Features:
 * - Multiple viewer instances with unique IDs
 * - Read-only mode with optimized viewer settings
 * - Theme management (individual and global)
 * - Graceful error handling
 * - Resource cleanup and disposal
 *
 * Usage Example:
 *
 *   // Create a viewer
 *   MonacoViewerInterop.createViewer('source-xml-container', 'sourceXml', {
 *       value: '<xml>...</xml>',
 *       language: 'xml',
 *       theme: 'vs-light'
 *   });
 *
 *   // Update content
 *   MonacoViewerInterop.setValue('sourceXml', '<xml>updated</xml>');
 *
 *   // Change theme for all viewers
 *   MonacoViewerInterop.setThemeAll('vs-dark');
 *
 *   // Cleanup
 *   MonacoViewerInterop.dispose('sourceXml');
 *   MonacoViewerInterop.disposeAll(); // Dispose all viewers
 *
 * @module MonacoViewerInterop
 */

window.MonacoViewerInterop = (function() {
    'use strict';

    /**
     * Private viewer registry
     * Map of viewerId -> Monaco Editor instance
     */
    const viewers = new Map();

    /**
     * Default viewer configuration
     * Optimized for read-only XML viewing
     */
    const DEFAULT_OPTIONS = {
        readOnly: true,
        domReadOnly: true,
        minimap: {
            enabled: false
        },
        lineNumbers: 'on',
        scrollBeyondLastLine: false,
        fontSize: 13,
        lineHeight: 20,
        automaticLayout: true,
        language: 'xml',
        theme: 'vs-light',
        // Additional read-only optimizations
        renderLineHighlight: 'none',
        cursorStyle: 'line',
        cursorBlinking: 'solid',
        contextmenu: false, // Disable context menu for viewers
        links: false, // Disable clickable links
        folding: true,
        glyphMargin: false, // No glyph margin needed for read-only
        wordWrap: 'on',
        scrollbar: {
            vertical: 'auto',
            horizontal: 'auto',
            useShadows: false,
            verticalScrollbarSize: 10,
            horizontalScrollbarSize: 10
        },
        // Disable unicode highlighting to prevent warning banner
        unicodeHighlight: {
            ambiguousCharacters: false,
            invisibleCharacters: false
        },
        renderWhitespace: 'none'
    };

    /**
     * Check if Monaco Editor is loaded
     * @returns {boolean} True if Monaco is available
     * @private
     */
    function isMonacoLoaded() {
        if (typeof monaco === 'undefined') {
            console.error('Monaco Editor is not loaded. Ensure monaco-editor library is included.');
            return false;
        }
        return true;
    }

    /**
     * Create a new Monaco viewer instance
     *
     * @param {string} elementId - DOM element ID where the viewer will be mounted
     * @param {string} viewerId - Unique identifier for this viewer instance
     * @param {object} options - Optional configuration overrides
     * @param {string} options.value - Initial content
     * @param {string} options.language - Language mode (default: 'xml')
     * @param {string} options.theme - Color theme (default: 'vs-light')
     * @returns {boolean} True if viewer was created successfully, false otherwise
     *
     * @example
     * MonacoViewerInterop.createViewer('xml-container', 'sourceXml', {
     *     value: '<root>content</root>',
     *     theme: 'vs-dark'
     * });
     */
    function createViewer(elementId, viewerId, options = {}) {
        // Validate Monaco is loaded
        if (!isMonacoLoaded()) {
            return false;
        }

        // Check if viewer already exists
        if (viewers.has(viewerId)) {
            console.warn(`Viewer '${viewerId}' already exists. Disposing old instance.`);
            dispose(viewerId);
        }

        // Validate DOM element exists
        const container = document.getElementById(elementId);
        if (!container) {
            console.error(`Container element '${elementId}' not found.`);
            return false;
        }

        try {
            // Merge user options with defaults
            const config = {
                ...DEFAULT_OPTIONS,
                ...options
            };

            // Create Monaco Editor instance in read-only mode
            const viewer = monaco.editor.create(container, config);

            // Store in registry
            viewers.set(viewerId, viewer);

            console.log(`Monaco viewer '${viewerId}' created successfully in element '${elementId}'`);
            return true;

        } catch (error) {
            console.error(`Error creating Monaco viewer '${viewerId}':`, error);
            return false;
        }
    }

    /**
     * Update the content of a specific viewer
     *
     * @param {string} viewerId - The viewer instance identifier
     * @param {string} content - New content to display
     * @returns {boolean} True if successful, false if viewer not found
     *
     * @example
     * MonacoViewerInterop.setValue('sourceXml', '<root>new content</root>');
     */
    function setValue(viewerId, content) {
        const viewer = viewers.get(viewerId);

        if (!viewer) {
            console.error(`Viewer '${viewerId}' not found.`);
            return false;
        }

        try {
            // Handle null/undefined content
            const safeContent = content ?? '';
            viewer.setValue(safeContent);

            console.log(`Viewer '${viewerId}' content updated (${safeContent.length} chars)`);
            return true;

        } catch (error) {
            console.error(`Error setting value for viewer '${viewerId}':`, error);
            return false;
        }
    }

    /**
     * Change the theme of a specific viewer
     *
     * @param {string} viewerId - The viewer instance identifier
     * @param {string} theme - Theme name ('vs-light' or 'vs-dark')
     * @returns {boolean} True if successful, false if viewer not found
     *
     * @example
     * MonacoViewerInterop.setTheme('sourceXml', 'vs-dark');
     */
    function setTheme(viewerId, theme) {
        if (!isMonacoLoaded()) {
            return false;
        }

        const viewer = viewers.get(viewerId);

        if (!viewer) {
            console.error(`Viewer '${viewerId}' not found.`);
            return false;
        }

        try {
            // Monaco's setTheme is a global method, but we validate the viewer exists
            monaco.editor.setTheme(theme);

            console.log(`Viewer '${viewerId}' theme set to '${theme}'`);
            return true;

        } catch (error) {
            console.error(`Error setting theme for viewer '${viewerId}':`, error);
            return false;
        }
    }

    /**
     * Change the theme for ALL registered viewers
     * Uses Monaco's global theme setter which applies to all editor instances
     *
     * @param {string} theme - Theme name ('vs-light' or 'vs-dark')
     * @returns {boolean} True if successful, false if Monaco not loaded
     *
     * @example
     * MonacoViewerInterop.setThemeAll('vs-dark');
     */
    function setThemeAll(theme) {
        if (!isMonacoLoaded()) {
            return false;
        }

        try {
            // Global theme change affects all editors
            monaco.editor.setTheme(theme);

            console.log(`Theme set to '${theme}' for all ${viewers.size} viewers`);
            return true;

        } catch (error) {
            console.error(`Error setting global theme:`, error);
            return false;
        }
    }

    /**
     * Dispose of a specific viewer instance
     * Releases Monaco Editor resources and removes from registry
     *
     * @param {string} viewerId - The viewer instance identifier
     * @returns {boolean} True if successful, false if viewer not found
     *
     * @example
     * MonacoViewerInterop.dispose('sourceXml');
     */
    function dispose(viewerId) {
        const viewer = viewers.get(viewerId);

        if (!viewer) {
            console.warn(`Viewer '${viewerId}' not found, nothing to dispose.`);
            return false;
        }

        try {
            viewer.dispose();
            viewers.delete(viewerId);

            console.log(`Viewer '${viewerId}' disposed successfully`);
            return true;

        } catch (error) {
            console.error(`Error disposing viewer '${viewerId}':`, error);
            // Still remove from registry even if dispose fails
            viewers.delete(viewerId);
            return false;
        }
    }

    /**
     * Dispose of all viewer instances
     * Used during component cleanup or page unload
     *
     * @returns {boolean} True if all viewers disposed successfully
     *
     * @example
     * MonacoViewerInterop.disposeAll();
     */
    function disposeAll() {
        console.log(`Disposing all ${viewers.size} viewers...`);

        let allSuccessful = true;
        const viewerIds = Array.from(viewers.keys());

        for (const viewerId of viewerIds) {
            if (!dispose(viewerId)) {
                allSuccessful = false;
            }
        }

        // Ensure map is cleared
        viewers.clear();

        console.log(`All viewers disposed. Success: ${allSuccessful}`);
        return allSuccessful;
    }

    /**
     * Get the number of active viewers
     * Useful for debugging and monitoring
     *
     * @returns {number} Count of active viewer instances
     *
     * @example
     * const count = MonacoViewerInterop.getViewerCount();
     */
    function getViewerCount() {
        return viewers.size;
    }

    /**
     * Get list of all active viewer IDs
     * Useful for debugging
     *
     * @returns {string[]} Array of viewer IDs
     *
     * @example
     * const ids = MonacoViewerInterop.getViewerIds();
     * console.log('Active viewers:', ids);
     */
    function getViewerIds() {
        return Array.from(viewers.keys());
    }

    /**
     * Check if a specific viewer exists
     *
     * @param {string} viewerId - The viewer instance identifier
     * @returns {boolean} True if viewer exists
     *
     * @example
     * if (MonacoViewerInterop.hasViewer('sourceXml')) {
     *     // Update existing viewer
     * }
     */
    function hasViewer(viewerId) {
        return viewers.has(viewerId);
    }

    /**
     * Trigger layout recalculation for a specific viewer
     * Useful after container resize
     *
     * @param {string} viewerId - The viewer instance identifier
     * @returns {boolean} True if successful
     *
     * @example
     * MonacoViewerInterop.layout('sourceXml');
     */
    function layout(viewerId) {
        const viewer = viewers.get(viewerId);

        if (!viewer) {
            console.error(`Viewer '${viewerId}' not found.`);
            return false;
        }

        try {
            viewer.layout();
            return true;
        } catch (error) {
            console.error(`Error laying out viewer '${viewerId}':`, error);
            return false;
        }
    }

    /**
     * Trigger layout recalculation for all viewers
     * Useful after global resize events
     *
     * @returns {boolean} True if all layouts successful
     *
     * @example
     * window.addEventListener('resize', () => {
     *     MonacoViewerInterop.layoutAll();
     * });
     */
    function layoutAll() {
        let allSuccessful = true;

        for (const [viewerId, viewer] of viewers.entries()) {
            try {
                viewer.layout();
            } catch (error) {
                console.error(`Error laying out viewer '${viewerId}':`, error);
                allSuccessful = false;
            }
        }

        return allSuccessful;
    }

    /**
     * Navigate to a specific line in the viewer
     * Scrolls to the line, sets cursor position, and highlights it temporarily
     *
     * @param {string} viewerId - The viewer instance identifier
     * @param {number} lineNumber - The line number to navigate to (1-based)
     * @returns {boolean} True if successful, false if viewer not found
     *
     * @example
     * MonacoViewerInterop.navigateToLine('outputXml', 42);
     */
    function navigateToLine(viewerId, lineNumber) {
        if (!isMonacoLoaded()) {
            return false;
        }

        const viewer = viewers.get(viewerId);

        if (!viewer) {
            console.error(`Viewer '${viewerId}' not found.`);
            return false;
        }

        try {
            // Scroll to line and center it in view
            viewer.revealLineInCenter(lineNumber);

            // Set cursor to the beginning of the line
            viewer.setPosition({ lineNumber: lineNumber, column: 1 });

            // Highlight the line temporarily with gold background
            const decorations = viewer.deltaDecorations([], [{
                range: new monaco.Range(lineNumber, 1, lineNumber, 1000),
                options: {
                    isWholeLine: true,
                    className: 'monaco-highlighted-line',
                    glyphMarginClassName: 'monaco-highlighted-glyph'
                }
            }]);

            // Remove highlight after 3 seconds
            setTimeout(() => {
                viewer.deltaDecorations(decorations, []);
            }, 3000);

            // Focus the editor
            viewer.focus();

            console.log(`Navigated to line ${lineNumber} in viewer '${viewerId}'`);
            return true;

        } catch (error) {
            console.error(`Error navigating to line in viewer '${viewerId}':`, error);
            return false;
        }
    }

    // Public API
    return {
        createViewer,
        setValue,
        setTheme,
        setThemeAll,
        dispose,
        disposeAll,
        getViewerCount,
        getViewerIds,
        hasViewer,
        layout,
        layoutAll,
        navigateToLine
    };
})();

/**
 * Cleanup on page unload
 * Automatically dispose all viewers to prevent memory leaks
 */
window.addEventListener('beforeunload', () => {
    if (window.MonacoViewerInterop) {
        window.MonacoViewerInterop.disposeAll();
    }
});

/**
 * Optional: Handle window resize
 * Automatically trigger layout recalculation for all viewers
 */
let resizeTimeout;
window.addEventListener('resize', () => {
    clearTimeout(resizeTimeout);
    resizeTimeout = setTimeout(() => {
        if (window.MonacoViewerInterop) {
            window.MonacoViewerInterop.layoutAll();
        }
    }, 250); // Debounce 250ms
});
