/**
 * Diff Viewer - Synchronized Scrolling
 * Handles synchronized scrolling between two diff panels
 */

// Track active scroll operations to prevent infinite loops
let isScrolling = false;
let scrollTimeout = null;

/**
 * Initialize synchronized scrolling for two panels
 * @param {string} leftPanelId - ID of the left panel element
 * @param {string} rightPanelId - ID of the right panel element
 */
export function initializeSyncScroll(leftPanelId, rightPanelId) {
    console.log('Initializing sync scroll for panels:', leftPanelId, rightPanelId);

    const leftPanel = document.getElementById(leftPanelId);
    const rightPanel = document.getElementById(rightPanelId);

    if (!leftPanel || !rightPanel) {
        console.error('Could not find panels:', { leftPanel: !!leftPanel, rightPanel: !!rightPanel });
        return;
    }

    // Enable synchronized scrolling by default
    enableSyncScroll(leftPanelId, rightPanelId);
}

/**
 * Enable synchronized scrolling between two panels
 * @param {string} leftPanelId - ID of the left panel element
 * @param {string} rightPanelId - ID of the right panel element
 */
export function enableSyncScroll(leftPanelId, rightPanelId) {
    const leftPanel = document.getElementById(leftPanelId);
    const rightPanel = document.getElementById(rightPanelId);

    if (!leftPanel || !rightPanel) {
        console.error('Could not find panels for enabling sync scroll');
        return;
    }

    // Remove any existing listeners first (to prevent duplicates)
    disableSyncScroll(leftPanelId, rightPanelId);

    // Create scroll handler for left panel
    const leftScrollHandler = function() {
        if (isScrolling) return;

        isScrolling = true;

        // Clear any existing timeout
        if (scrollTimeout) {
            clearTimeout(scrollTimeout);
        }

        // Sync the right panel to match left panel's scroll position
        const scrollPercentage = leftPanel.scrollTop / (leftPanel.scrollHeight - leftPanel.clientHeight);
        const targetScrollTop = scrollPercentage * (rightPanel.scrollHeight - rightPanel.clientHeight);
        rightPanel.scrollTop = targetScrollTop;

        // Reset the flag after a short delay
        scrollTimeout = setTimeout(() => {
            isScrolling = false;
        }, 50);
    };

    // Create scroll handler for right panel
    const rightScrollHandler = function() {
        if (isScrolling) return;

        isScrolling = true;

        // Clear any existing timeout
        if (scrollTimeout) {
            clearTimeout(scrollTimeout);
        }

        // Sync the left panel to match right panel's scroll position
        const scrollPercentage = rightPanel.scrollTop / (rightPanel.scrollHeight - rightPanel.clientHeight);
        const targetScrollTop = scrollPercentage * (leftPanel.scrollHeight - leftPanel.clientHeight);
        leftPanel.scrollTop = targetScrollTop;

        // Reset the flag after a short delay
        scrollTimeout = setTimeout(() => {
            isScrolling = false;
        }, 50);
    };

    // Store handlers on the elements so we can remove them later
    leftPanel._syncScrollHandler = leftScrollHandler;
    rightPanel._syncScrollHandler = rightScrollHandler;

    // Add event listeners
    leftPanel.addEventListener('scroll', leftScrollHandler, { passive: true });
    rightPanel.addEventListener('scroll', rightScrollHandler, { passive: true });

    console.log('Synchronized scrolling enabled');
}

/**
 * Disable synchronized scrolling between two panels
 * @param {string} leftPanelId - ID of the left panel element
 * @param {string} rightPanelId - ID of the right panel element
 */
export function disableSyncScroll(leftPanelId, rightPanelId) {
    const leftPanel = document.getElementById(leftPanelId);
    const rightPanel = document.getElementById(rightPanelId);

    if (!leftPanel || !rightPanel) {
        return;
    }

    // Remove event listeners if they exist
    if (leftPanel._syncScrollHandler) {
        leftPanel.removeEventListener('scroll', leftPanel._syncScrollHandler);
        delete leftPanel._syncScrollHandler;
    }

    if (rightPanel._syncScrollHandler) {
        rightPanel.removeEventListener('scroll', rightPanel._syncScrollHandler);
        delete rightPanel._syncScrollHandler;
    }

    console.log('Synchronized scrolling disabled');
}
