// Keyboard Shortcuts Handler
window.KeyboardShortcuts = {
    dotNetRef: null,
    helpVisible: false,

    initialize: function(dotNetReference) {
        this.dotNetRef = dotNetReference;

        document.addEventListener('keydown', (e) => {
            // Ctrl+S - Save XSLT
            if (e.ctrlKey && e.key === 's') {
                e.preventDefault();
                this.dotNetRef.invokeMethodAsync('OnSaveShortcut');
            }

            // Ctrl+Enter - Transform
            else if (e.ctrlKey && e.key === 'Enter') {
                e.preventDefault();
                this.dotNetRef.invokeMethodAsync('OnTransformShortcut');
            }

            // Ctrl+O - Focus project selector
            else if (e.ctrlKey && e.key === 'o') {
                e.preventDefault();
                const projectSelect = document.querySelector('.toolbar-select');
                if (projectSelect) {
                    projectSelect.focus();
                }
            }

            // F11 - Toggle fullscreen editor
            else if (e.key === 'F11') {
                e.preventDefault();
                this.toggleFullscreen();
            }

            // ? - Show help
            else if (e.key === '?' && !e.ctrlKey && !e.shiftKey) {
                e.preventDefault();
                this.dotNetRef.invokeMethodAsync('OnShowHelp');
            }
        });

        console.log('Keyboard shortcuts initialized');
    },

    toggleFullscreen: function() {
        const editorContainer = document.getElementById('monaco-editor-container');
        if (!editorContainer) return;

        if (!document.fullscreenElement) {
            editorContainer.parentElement.requestFullscreen().catch(err => {
                console.error('Error attempting to enable fullscreen:', err);
            });
        } else {
            document.exitFullscreen();
        }
    },

    dispose: function() {
        // Cleanup would go here if needed
        console.log('Keyboard shortcuts disposed');
    }
};

// Theme Management
window.ThemeManager = {
    initialize: function(initialTheme) {
        const theme = initialTheme || this.getSavedTheme() || 'light';
        this.applyTheme(theme);
    },

    applyTheme: function(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem('preferred-theme', theme);

        // Update Monaco editor theme if it exists
        if (window.MonacoEditorInterop && window.MonacoEditorInterop.editor) {
            const monacoTheme = theme === 'dark' ? 'vs-dark' : 'vs-light';
            window.monaco.editor.setTheme(monacoTheme);
        }
    },

    toggleTheme: function() {
        const currentTheme = document.documentElement.getAttribute('data-theme') || 'light';
        const newTheme = currentTheme === 'light' ? 'dark' : 'light';
        this.applyTheme(newTheme);
        return newTheme;
    },

    getSavedTheme: function() {
        return localStorage.getItem('preferred-theme');
    },

    getCurrentTheme: function() {
        return document.documentElement.getAttribute('data-theme') || 'light';
    }
};

// Touch Gesture Support for Mobile
window.TouchGestures = {
    startX: 0,
    startY: 0,
    currentPanel: null,

    initialize: function() {
        const panels = document.querySelectorAll('.panel');

        panels.forEach(panel => {
            panel.addEventListener('touchstart', (e) => {
                this.startX = e.touches[0].clientX;
                this.startY = e.touches[0].clientY;
                this.currentPanel = panel;
            });

            panel.addEventListener('touchmove', (e) => {
                if (!this.currentPanel) return;

                const deltaX = e.touches[0].clientX - this.startX;
                const deltaY = e.touches[0].clientY - this.startY;

                // Horizontal swipe to resize panels
                if (Math.abs(deltaX) > Math.abs(deltaY) && Math.abs(deltaX) > 50) {
                    // Handle panel resize
                    console.log('Horizontal swipe detected:', deltaX);
                }
            });

            panel.addEventListener('touchend', () => {
                this.currentPanel = null;
            });
        });

        console.log('Touch gestures initialized');
    }
};

// Tooltip Management
window.TooltipManager = {
    initialize: function() {
        // Initialize Bootstrap tooltips
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });

        console.log('Tooltips initialized');
    },

    refresh: function() {
        // Dispose old tooltips
        const tooltips = document.querySelectorAll('.tooltip');
        tooltips.forEach(tooltip => tooltip.remove());

        // Reinitialize
        this.initialize();
    }
};

// Loading State Manager
window.LoadingManager = {
    show: function(elementId, message) {
        const element = document.getElementById(elementId);
        if (!element) return;

        const spinner = document.createElement('div');
        spinner.className = 'loading-overlay';
        spinner.innerHTML = `
            <div class="loading-spinner">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
                ${message ? `<p class="mt-3">${message}</p>` : ''}
            </div>
        `;

        element.style.position = 'relative';
        element.appendChild(spinner);
    },

    hide: function(elementId) {
        const element = document.getElementById(elementId);
        if (!element) return;

        const overlay = element.querySelector('.loading-overlay');
        if (overlay) {
            overlay.remove();
        }
    }
};

// Add loading overlay styles
const loadingStyles = `
<style>
.loading-overlay {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: rgba(255, 255, 255, 0.9);
    backdrop-filter: blur(4px);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 100;
    animation: fadeIn 0.2s ease-in;
}

[data-theme="dark"] .loading-overlay {
    background: rgba(26, 29, 35, 0.9);
}

.loading-spinner {
    text-align: center;
}

.loading-spinner .spinner-border {
    width: 3rem;
    height: 3rem;
    border-width: 0.3em;
}

.loading-spinner p {
    margin: 0;
    color: #6c757d;
    font-weight: 500;
}

[data-theme="dark"] .loading-spinner p {
    color: #adb5bd;
}
</style>
`;

// Inject loading styles
if (document.head) {
    document.head.insertAdjacentHTML('beforeend', loadingStyles);
}

// Visual Feedback Manager
window.VisualFeedback = {
    triggerTransformSuccess: function() {
        // Find the transform button in the toolbar
        const transformButton = document.querySelector('.nav-toolbar-actions .btn-light');
        if (!transformButton) {
            console.warn('Transform button not found');
            return;
        }

        // Add the success glow class
        transformButton.classList.add('transform-success');

        // Remove the class after animation completes
        setTimeout(() => {
            transformButton.classList.remove('transform-success');
        }, 1000);
    }
};
