/**
 * Diff Viewer - Synchronized Scrolling
 * Handles synchronized scrolling between two diff panels
 */

// Track active scroll operations to prevent infinite loops
let isScrolling = false;
let scrollTimeout = null;
let finalAlignmentTimeout = null;

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
 * Find the index of the line currently at the top of the visible area
 * @param {HTMLElement} panel - The scrollable panel element
 * @returns {number} The 0-based line index
 */
function findLineIndexAtScroll(panel) {
    const lines = panel.querySelectorAll('.diff-line');
    if (lines.length === 0) return 0;

    const scrollTop = panel.scrollTop;

    console.log(`Finding line at scrollTop ${scrollTop}, total lines: ${lines.length}`);

    // Find the line whose top is closest to the current scroll position
    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];
        const lineTop = line.offsetTop;
        const lineBottom = lineTop + line.offsetHeight;

        // If this line is visible at the top (its bottom edge is below the scroll position)
        if (lineBottom > scrollTop) {
            console.log(`Found line index ${i} at offsetTop ${lineTop}, lineBottom ${lineBottom}`);
            return i;
        }
    }

    // If we've scrolled past all lines, return the last line
    const lastIndex = Math.max(0, lines.length - 1);
    console.log(`Defaulting to last line index ${lastIndex}`);
    return lastIndex;
}

/**
 * Scroll panel to show the line at the specified index at the top
 * @param {HTMLElement} panel - The scrollable panel element
 * @param {number} lineIndex - The 0-based line index to scroll to
 */
function scrollToLineIndex(panel, lineIndex) {
    const lines = panel.querySelectorAll('.diff-line');
    if (lines.length === 0 || lineIndex < 0) {
        console.log('Cannot scroll: no lines or invalid index');
        return;
    }

    // Clamp lineIndex to valid range
    const targetIndex = Math.min(lineIndex, lines.length - 1);
    const targetLine = lines[targetIndex];

    if (targetLine) {
        const targetScrollTop = targetLine.offsetTop;
        console.log(`Scrolling to line index ${targetIndex}, offsetTop ${targetScrollTop}`);
        panel.scrollTop = targetScrollTop;
    }
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

        // Clear any pending final alignment
        if (finalAlignmentTimeout) {
            clearTimeout(finalAlignmentTimeout);
        }

        // Immediate synchronization (may be slightly imperfect during rapid scrolling)
        console.log('Left panel scrolled, syncing right panel...');
        const lineIndex = findLineIndexAtScroll(leftPanel);
        scrollToLineIndex(rightPanel, lineIndex);

        // Reset the flag after a short delay
        scrollTimeout = setTimeout(() => {
            isScrolling = false;
        }, 50);

        // Schedule final alignment after user stops scrolling (200ms debounce)
        finalAlignmentTimeout = setTimeout(() => {
            console.log('Final alignment: left → right');
            // Set flag to prevent the triggered scroll event from starting another sync cycle
            isScrolling = true;
            const finalLineIndex = findLineIndexAtScroll(leftPanel);
            scrollToLineIndex(rightPanel, finalLineIndex);
            // Clear flag after a brief delay
            setTimeout(() => {
                isScrolling = false;
            }, 50);
        }, 200);
    };

    // Create scroll handler for right panel
    const rightScrollHandler = function() {
        if (isScrolling) return;

        isScrolling = true;

        // Clear any existing timeout
        if (scrollTimeout) {
            clearTimeout(scrollTimeout);
        }

        // Clear any pending final alignment
        if (finalAlignmentTimeout) {
            clearTimeout(finalAlignmentTimeout);
        }

        // Immediate synchronization (may be slightly imperfect during rapid scrolling)
        console.log('Right panel scrolled, syncing left panel...');
        const lineIndex = findLineIndexAtScroll(rightPanel);
        scrollToLineIndex(leftPanel, lineIndex);

        // Reset the flag after a short delay
        scrollTimeout = setTimeout(() => {
            isScrolling = false;
        }, 50);

        // Schedule final alignment after user stops scrolling (200ms debounce)
        finalAlignmentTimeout = setTimeout(() => {
            console.log('Final alignment: right → left');
            // Set flag to prevent the triggered scroll event from starting another sync cycle
            isScrolling = true;
            const finalLineIndex = findLineIndexAtScroll(rightPanel);
            scrollToLineIndex(leftPanel, finalLineIndex);
            // Clear flag after a brief delay
            setTimeout(() => {
                isScrolling = false;
            }, 50);
        }, 200);
    };

    // Store handlers on the elements so we can remove them later
    leftPanel._syncScrollHandler = leftScrollHandler;
    rightPanel._syncScrollHandler = rightScrollHandler;

    // Add event listeners
    leftPanel.addEventListener('scroll', leftScrollHandler, { passive: true });
    rightPanel.addEventListener('scroll', rightScrollHandler, { passive: true });

    console.log('Synchronized scrolling enabled (line-based algorithm)');
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

    // Clear any pending timeouts
    if (scrollTimeout) {
        clearTimeout(scrollTimeout);
        scrollTimeout = null;
    }

    if (finalAlignmentTimeout) {
        clearTimeout(finalAlignmentTimeout);
        finalAlignmentTimeout = null;
    }

    console.log('Synchronized scrolling disabled');
}
