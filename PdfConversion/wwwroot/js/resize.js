// Panel Resize Functionality

window.PanelResize = {
    init: function () {
        const container = document.getElementById('panels-container');
        if (!container) {
            console.error('Panel resize: container not found');
            return;
        }

        const leftPanel = document.getElementById('panel-left');
        const centerPanel = document.getElementById('panel-center');
        const rightPanel = document.getElementById('panel-right');
        const handle1 = document.getElementById('resize-handle-1');
        const handle2 = document.getElementById('resize-handle-2');

        if (!leftPanel || !centerPanel || !rightPanel || !handle1 || !handle2) {
            console.error('Panel resize: missing elements', {
                leftPanel: !!leftPanel,
                centerPanel: !!centerPanel,
                rightPanel: !!rightPanel,
                handle1: !!handle1,
                handle2: !!handle2
            });
            return;
        }

        // Load saved widths from localStorage
        const savedWidths = this.loadWidths();
        if (savedWidths) {
            leftPanel.style.flex = `0 0 ${savedWidths.left}px`;
            centerPanel.style.flex = `0 0 ${savedWidths.center}px`;
            rightPanel.style.flex = `1 1 auto`;
        }

        // Handle 1: Resize between left and center panels
        this.initializeHandle(handle1, leftPanel, centerPanel, container);

        // Handle 2: Resize between center and right panels
        this.initializeHandle(handle2, centerPanel, rightPanel, container);

        // Add window resize listener
        window.addEventListener('resize', () => {
            this.handleWindowResize();
        });

        // Apply initial adjustments
        this.handleWindowResize();

        console.log('Panel resize initialized');
    },

    initializeHandle: function (handle, leftPanel, rightPanel, container) {
        let isResizing = false;
        let startX = 0;
        let startLeftWidth = 0;
        let startRightWidth = 0;

        handle.addEventListener('mousedown', (e) => {
            isResizing = true;
            startX = e.clientX;
            startLeftWidth = leftPanel.offsetWidth;
            startRightWidth = rightPanel.offsetWidth;

            // Prevent text selection during resize
            document.body.style.userSelect = 'none';
            document.body.style.cursor = 'col-resize';

            e.preventDefault();
        });

        document.addEventListener('mousemove', (e) => {
            if (!isResizing) return;

            const deltaX = e.clientX - startX;
            const containerWidth = container.offsetWidth;
            const minWidth = 200; // Minimum panel width

            const newLeftWidth = startLeftWidth + deltaX;
            const newRightWidth = startRightWidth - deltaX;

            // Enforce minimum widths
            if (newLeftWidth >= minWidth && newRightWidth >= minWidth) {
                // Calculate percentages to maintain responsive behavior
                const leftPercent = (newLeftWidth / containerWidth) * 100;
                const rightPercent = (newRightWidth / containerWidth) * 100;

                leftPanel.style.flex = `0 0 ${newLeftWidth}px`;
                rightPanel.style.flex = `0 0 ${newRightWidth}px`;

                // Trigger Monaco editor resize if it exists
                if (window.MonacoEditorInterop && window.MonacoEditorInterop.resize) {
                    window.MonacoEditorInterop.resize();
                }
            }
        });

        document.addEventListener('mouseup', () => {
            if (isResizing) {
                isResizing = false;
                document.body.style.userSelect = '';
                document.body.style.cursor = '';

                // Save widths to localStorage
                this.saveWidths();
            }
        });
    },

    saveWidths: function () {
        const leftPanel = document.getElementById('panel-left');
        const centerPanel = document.getElementById('panel-center');
        const rightPanel = document.getElementById('panel-right');

        if (!leftPanel || !centerPanel || !rightPanel) return;

        const widths = {
            left: leftPanel.offsetWidth,
            center: centerPanel.offsetWidth,
            right: rightPanel.offsetWidth
        };

        localStorage.setItem('panel-widths', JSON.stringify(widths));
    },

    loadWidths: function () {
        const saved = localStorage.getItem('panel-widths');
        if (saved) {
            try {
                return JSON.parse(saved);
            } catch (e) {
                console.error('Error parsing saved panel widths:', e);
                return null;
            }
        }
        return null;
    },

    reset: function () {
        localStorage.removeItem('panel-widths');
        const leftPanel = document.getElementById('panel-left');
        const centerPanel = document.getElementById('panel-center');
        const rightPanel = document.getElementById('panel-right');

        if (leftPanel && centerPanel && rightPanel) {
            leftPanel.style.flex = '1';
            centerPanel.style.flex = '1';
            rightPanel.style.flex = '1';
        }
    },

    handleWindowResize: function () {
        const container = document.getElementById('panels-container');
        if (!container) return;

        // Check if panels are stacked vertically (based on CSS media query)
        const isStacked = window.innerWidth <= 1200;

        if (isStacked) {
            // Vertical layout - ensure min heights
            this.adjustStackedPanels();
        } else {
            // Horizontal layout - recalculate widths to prevent disappearing
            this.adjustHorizontalPanels();
        }
    },

    adjustHorizontalPanels: function () {
        const container = document.getElementById('panels-container');
        const leftPanel = document.getElementById('panel-left');
        const centerPanel = document.getElementById('panel-center');
        const rightPanel = document.getElementById('panel-right');

        if (!container || !leftPanel || !centerPanel || !rightPanel) return;

        const containerWidth = container.offsetWidth;
        const minWidth = 200;
        const totalMinWidth = minWidth * 3;

        // Get current widths
        const leftWidth = leftPanel.offsetWidth;
        const centerWidth = centerPanel.offsetWidth;
        const rightWidth = rightPanel.offsetWidth;
        const currentTotal = leftWidth + centerWidth + rightWidth;

        // If container is too small to fit minimum widths, scale proportionally
        if (containerWidth < totalMinWidth) {
            const scale = containerWidth / totalMinWidth;
            leftPanel.style.flex = `0 0 ${minWidth * scale}px`;
            centerPanel.style.flex = `0 0 ${minWidth * scale}px`;
            rightPanel.style.flex = `0 0 ${minWidth * scale}px`;
            console.log('Panel resize: Scaling down panels to fit container', { containerWidth, scale });
            return;
        }

        // If panels exceed container width, scale down proportionally
        if (currentTotal > containerWidth) {
            const scale = (containerWidth - 20) / currentTotal; // -20 for some breathing room
            const newLeftWidth = Math.max(minWidth, leftWidth * scale);
            const newCenterWidth = Math.max(minWidth, centerWidth * scale);

            leftPanel.style.flex = `0 0 ${newLeftWidth}px`;
            centerPanel.style.flex = `0 0 ${newCenterWidth}px`;
            rightPanel.style.flex = `1 1 auto`;

            console.log('Panel resize: Scaled panels to fit container', {
                containerWidth,
                oldTotal: currentTotal,
                scale
            });
        } else if (leftWidth < minWidth || centerWidth < minWidth || rightWidth < minWidth) {
            // Ensure minimum widths are respected
            leftPanel.style.flex = `0 0 ${Math.max(minWidth, leftWidth)}px`;
            centerPanel.style.flex = `0 0 ${Math.max(minWidth, centerWidth)}px`;
            rightPanel.style.flex = `1 1 auto`;

            console.log('Panel resize: Enforced minimum widths');
        }

        // Trigger Monaco editor resize if it exists
        if (window.MonacoEditorInterop && window.MonacoEditorInterop.resize) {
            window.MonacoEditorInterop.resize();
        }
    },

    adjustStackedPanels: function () {
        const leftPanel = document.getElementById('panel-left');
        const centerPanel = document.getElementById('panel-center');
        const rightPanel = document.getElementById('panel-right');

        if (!leftPanel || !centerPanel || !rightPanel) return;

        // Ensure minimum heights when stacked vertically
        const minHeight = 200;

        [leftPanel, centerPanel, rightPanel].forEach(panel => {
            // Only enforce if height is too small
            if (panel.offsetHeight < minHeight) {
                panel.style.minHeight = `${minHeight}px`;
            }
        });

        console.log('Panel resize: Adjusted stacked panel heights');

        // Trigger Monaco editor resize if it exists
        if (window.MonacoEditorInterop && window.MonacoEditorInterop.resize) {
            window.MonacoEditorInterop.resize();
        }
    }
};

// Initialize after Blazor is ready
function initializeWhenReady() {
    // Wait for Blazor to render the panels
    const checkInterval = setInterval(() => {
        const container = document.getElementById('panels-container');
        if (container && container.children.length > 0) {
            clearInterval(checkInterval);
            console.log('Panels found, initializing resize...');
            window.PanelResize.init();
        }
    }, 100);

    // Timeout after 5 seconds
    setTimeout(() => {
        clearInterval(checkInterval);
        console.warn('Panel resize initialization timeout');
    }, 5000);
}

// Try to initialize on page load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeWhenReady);
} else {
    initializeWhenReady();
}

// Re-initialize after Blazor updates
document.addEventListener('DOMContentLoaded', () => {
    // Listen for Blazor reconnection/update events
    if (typeof MutationObserver !== 'undefined') {
        const observer = new MutationObserver((mutations) => {
            for (const mutation of mutations) {
                if (mutation.addedNodes.length > 0) {
                    const container = document.getElementById('panels-container');
                    if (container && !container.hasAttribute('data-resize-initialized')) {
                        console.log('Panels re-rendered, re-initializing resize...');
                        container.setAttribute('data-resize-initialized', 'true');
                        window.PanelResize.init();
                        break;
                    }
                }
            }
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }
});
