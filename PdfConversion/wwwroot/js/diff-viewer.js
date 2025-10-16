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

    // Find the line whose top is closest to the current scroll position
    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];
        const lineTop = line.offsetTop;
        const lineBottom = lineTop + line.offsetHeight;

        // If this line is visible at the top (its bottom edge is below the scroll position)
        if (lineBottom > scrollTop) {
            return i;
        }
    }

    // If we've scrolled past all lines, return the last line
    const lastIndex = Math.max(0, lines.length - 1);
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
        return;
    }

    // Clamp lineIndex to valid range
    const targetIndex = Math.min(lineIndex, lines.length - 1);
    const targetLine = lines[targetIndex];

    if (targetLine) {
        const targetScrollTop = targetLine.offsetTop;
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
        const lineIndex = findLineIndexAtScroll(leftPanel);
        scrollToLineIndex(rightPanel, lineIndex);

        // Reset the flag after a short delay
        scrollTimeout = setTimeout(() => {
            isScrolling = false;
        }, 50);

        // Schedule final alignment after user stops scrolling (200ms debounce)
        finalAlignmentTimeout = setTimeout(() => {
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
        const lineIndex = findLineIndexAtScroll(rightPanel);
        scrollToLineIndex(leftPanel, lineIndex);

        // Reset the flag after a short delay
        scrollTimeout = setTimeout(() => {
            isScrolling = false;
        }, 50);

        // Schedule final alignment after user stops scrolling (200ms debounce)
        finalAlignmentTimeout = setTimeout(() => {
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
}

/**
 * Get all difference line indices (modified, inserted, deleted)
 * @param {HTMLElement} panel - The panel element
 * @returns {number[]} Array of line indices that have differences
 */
function getDifferenceLineIndices(panel) {
    const lines = panel.querySelectorAll('.diff-line');
    const differenceIndices = [];

    lines.forEach((line, index) => {
        // Check if line has any difference class (not unchanged or imaginary)
        if (!line.classList.contains('line-unchanged') &&
            !line.classList.contains('line-imaginary')) {
            differenceIndices.push(index);
        }
    });

    return differenceIndices;
}

/**
 * Jump to the next difference from the current scroll position
 * @param {string} leftPanelId - ID of the left panel element
 * @param {string} rightPanelId - ID of the right panel element
 * @returns {boolean} True if a difference was found and scrolled to, false otherwise
 */
export function jumpToNextDifference(leftPanelId, rightPanelId) {
    const leftPanel = document.getElementById(leftPanelId);
    const rightPanel = document.getElementById(rightPanelId);

    if (!leftPanel || !rightPanel) {
        console.error('Could not find panels for navigation');
        return false;
    }

    // Get current scroll position (use left panel as reference)
    const currentLineIndex = findLineIndexAtScroll(leftPanel);

    // Get all difference indices
    const differenceIndices = getDifferenceLineIndices(leftPanel);

    if (differenceIndices.length === 0) {
        return false;
    }

    // Find the next difference after current position
    const nextDifferenceIndex = differenceIndices.find(index => index > currentLineIndex);

    if (nextDifferenceIndex === undefined) {
        return false;
    }

    // Temporarily disable sync to prevent infinite loop
    isScrolling = true;

    // Scroll both panels to the next difference
    scrollToLineIndex(leftPanel, nextDifferenceIndex);
    scrollToLineIndex(rightPanel, nextDifferenceIndex);

    // Re-enable sync after a short delay
    setTimeout(() => {
        isScrolling = false;
    }, 100);

    // Highlight the difference briefly
    highlightDifference(leftPanel, nextDifferenceIndex);
    highlightDifference(rightPanel, nextDifferenceIndex);

    return true;
}

/**
 * Jump to the previous difference from the current scroll position
 * @param {string} leftPanelId - ID of the left panel element
 * @param {string} rightPanelId - ID of the right panel element
 * @returns {boolean} True if a difference was found and scrolled to, false otherwise
 */
export function jumpToPreviousDifference(leftPanelId, rightPanelId) {
    const leftPanel = document.getElementById(leftPanelId);
    const rightPanel = document.getElementById(rightPanelId);

    if (!leftPanel || !rightPanel) {
        console.error('Could not find panels for navigation');
        return false;
    }

    // Get current scroll position (use left panel as reference)
    const currentLineIndex = findLineIndexAtScroll(leftPanel);

    // Get all difference indices
    const differenceIndices = getDifferenceLineIndices(leftPanel);

    if (differenceIndices.length === 0) {
        return false;
    }

    // Find the previous difference before current position
    // Reverse the array to find the last one that's less than current
    const previousDifferenceIndex = [...differenceIndices].reverse().find(index => index < currentLineIndex);

    if (previousDifferenceIndex === undefined) {
        return false;
    }

    // Temporarily disable sync to prevent infinite loop
    isScrolling = true;

    // Scroll both panels to the previous difference
    scrollToLineIndex(leftPanel, previousDifferenceIndex);
    scrollToLineIndex(rightPanel, previousDifferenceIndex);

    // Re-enable sync after a short delay
    setTimeout(() => {
        isScrolling = false;
    }, 100);

    // Highlight the difference briefly
    highlightDifference(leftPanel, previousDifferenceIndex);
    highlightDifference(rightPanel, previousDifferenceIndex);

    return true;
}

/**
 * Briefly highlight a difference line
 * @param {HTMLElement} panel - The panel element
 * @param {number} lineIndex - The line index to highlight
 */
function highlightDifference(panel, lineIndex) {
    const lines = panel.querySelectorAll('.diff-line');
    if (lineIndex < 0 || lineIndex >= lines.length) return;

    const line = lines[lineIndex];

    // Add highlight class
    line.classList.add('diff-line-highlighted');

    // Remove highlight after 1 second
    setTimeout(() => {
        line.classList.remove('diff-line-highlighted');
    }, 1000);
}
