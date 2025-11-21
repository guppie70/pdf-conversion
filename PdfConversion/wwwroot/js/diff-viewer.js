/**
 * Diff Viewer - Synchronized Scrolling
 * Handles synchronized scrolling between two diff panels
 */

// Track active scroll operations to prevent infinite loops
let isScrolling = false;
let scrollTimeout = null;
let finalAlignmentTimeout = null;

// Track the current anchor line for navigation
let currentAnchorLine = 0;

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

    // Initialize line click handlers for anchor navigation
    initializeLineClickHandlers(leftPanelId, rightPanelId);
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
 * Scroll panel to show the line at the specified index, centered or near the top
 * @param {HTMLElement} panel - The scrollable panel element
 * @param {number} lineIndex - The 0-based line index to scroll to
 */
function scrollToLineIndexCentered(panel, lineIndex) {
    const lines = panel.querySelectorAll('.diff-line');
    if (lines.length === 0 || lineIndex < 0) {
        return;
    }

    // Clamp lineIndex to valid range
    const targetIndex = Math.min(lineIndex, lines.length - 1);
    const targetLine = lines[targetIndex];

    if (targetLine) {
        // Get panel height and target line position
        const panelHeight = panel.clientHeight;
        const lineTop = targetLine.offsetTop;
        const lineHeight = targetLine.offsetHeight;

        // Try to center the line, but keep it near top if at beginning
        const offset = Math.max(0, (panelHeight / 3) - (lineHeight / 2));
        const targetScrollTop = Math.max(0, lineTop - offset);

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
 * Checks BOTH panels to catch all difference types:
 * - Deleted lines only appear in left panel
 * - Inserted lines only appear in right panel
 * - Modified lines appear in both panels
 * @param {string} leftPanelId - ID of the left panel element
 * @param {string} rightPanelId - ID of the right panel element
 * @returns {number[]} Array of line indices that have differences in either panel
 */
function getDifferenceLineIndices(leftPanelId, rightPanelId) {
    const leftPanel = document.getElementById(leftPanelId);
    const rightPanel = document.getElementById(rightPanelId);

    if (!leftPanel || !rightPanel) {
        console.error('Could not find panels for difference detection');
        return [];
    }

    const leftLines = leftPanel.querySelectorAll('.diff-line');
    const rightLines = rightPanel.querySelectorAll('.diff-line');
    const differenceIndices = [];

    // Check each line index in both panels
    const maxLines = Math.max(leftLines.length, rightLines.length);
    for (let index = 0; index < maxLines; index++) {
        const leftLine = leftLines[index];
        const rightLine = rightLines[index];

        // Check if either panel has a difference at this index
        const leftHasDiff = leftLine &&
            !leftLine.classList.contains('line-unchanged') &&
            !leftLine.classList.contains('line-imaginary');

        const rightHasDiff = rightLine &&
            !rightLine.classList.contains('line-unchanged') &&
            !rightLine.classList.contains('line-imaginary');

        if (leftHasDiff || rightHasDiff) {
            differenceIndices.push(index);
        }
    }

    return differenceIndices;
}

/**
 * Jump to the next difference from the current anchor line
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

    // Get all difference indices from BOTH panels
    const differenceIndices = getDifferenceLineIndices(leftPanelId, rightPanelId);

    if (differenceIndices.length === 0) {
        return false;
    }

    // Find the next difference after current anchor line
    const nextDifferenceIndex = differenceIndices.find(index => index > currentAnchorLine);

    if (nextDifferenceIndex === undefined) {
        console.log('No more differences after line', currentAnchorLine);
        return false;
    }

    // Temporarily disable sync to prevent infinite loop
    isScrolling = true;

    // Scroll both panels to the next difference (centered)
    scrollToLineIndexCentered(leftPanel, nextDifferenceIndex);
    scrollToLineIndexCentered(rightPanel, nextDifferenceIndex);

    // Re-enable sync after a short delay
    setTimeout(() => {
        isScrolling = false;
    }, 100);

    // Set this as the new anchor line
    setAnchorLine(leftPanelId, rightPanelId, nextDifferenceIndex);

    // Highlight the difference briefly
    highlightDifference(leftPanel, nextDifferenceIndex);
    highlightDifference(rightPanel, nextDifferenceIndex);

    return true;
}

/**
 * Jump to the previous difference from the current anchor line
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

    // Get all difference indices from BOTH panels
    const differenceIndices = getDifferenceLineIndices(leftPanelId, rightPanelId);

    if (differenceIndices.length === 0) {
        return false;
    }

    // Find the previous difference before current anchor line
    // Reverse the array to find the last one that's less than current anchor
    const previousDifferenceIndex = [...differenceIndices].reverse().find(index => index < currentAnchorLine);

    if (previousDifferenceIndex === undefined) {
        console.log('No more differences before line', currentAnchorLine);
        return false;
    }

    // Temporarily disable sync to prevent infinite loop
    isScrolling = true;

    // Scroll both panels to the previous difference (centered)
    scrollToLineIndexCentered(leftPanel, previousDifferenceIndex);
    scrollToLineIndexCentered(rightPanel, previousDifferenceIndex);

    // Re-enable sync after a short delay
    setTimeout(() => {
        isScrolling = false;
    }, 100);

    // Set this as the new anchor line
    setAnchorLine(leftPanelId, rightPanelId, previousDifferenceIndex);

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

/**
 * Set the anchor line for navigation
 * @param {string} leftPanelId - ID of the left panel element
 * @param {string} rightPanelId - ID of the right panel element
 * @param {number} lineIndex - The line index to set as anchor
 */
export function setAnchorLine(leftPanelId, rightPanelId, lineIndex) {
    const leftPanel = document.getElementById(leftPanelId);
    const rightPanel = document.getElementById(rightPanelId);

    if (!leftPanel || !rightPanel) {
        console.error('Could not find panels for setting anchor');
        return;
    }

    // Update the anchor line
    currentAnchorLine = lineIndex;

    // Remove anchor class from all lines in both panels
    const allLines = [
        ...leftPanel.querySelectorAll('.diff-line'),
        ...rightPanel.querySelectorAll('.diff-line')
    ];

    allLines.forEach(line => line.classList.remove('diff-line-anchor'));

    // Add anchor class to the specified line in both panels
    const leftLines = leftPanel.querySelectorAll('.diff-line');
    const rightLines = rightPanel.querySelectorAll('.diff-line');

    if (lineIndex >= 0 && lineIndex < leftLines.length) {
        leftLines[lineIndex].classList.add('diff-line-anchor');
    }

    if (lineIndex >= 0 && lineIndex < rightLines.length) {
        rightLines[lineIndex].classList.add('diff-line-anchor');
    }

    console.log('Anchor line set to:', lineIndex);
}

/**
 * Initialize click handlers on all diff lines for anchor navigation
 * @param {string} leftPanelId - ID of the left panel element
 * @param {string} rightPanelId - ID of the right panel element
 */
export function initializeLineClickHandlers(leftPanelId, rightPanelId) {
    const leftPanel = document.getElementById(leftPanelId);
    const rightPanel = document.getElementById(rightPanelId);

    if (!leftPanel || !rightPanel) {
        console.error('Could not find panels for click handlers');
        return;
    }

    // Add click handlers to left panel lines
    const leftLines = leftPanel.querySelectorAll('.diff-line');
    leftLines.forEach((line, index) => {
        line.style.cursor = 'pointer';
        line.addEventListener('click', () => {
            setAnchorLine(leftPanelId, rightPanelId, index);
        });
    });

    // Add click handlers to right panel lines
    const rightLines = rightPanel.querySelectorAll('.diff-line');
    rightLines.forEach((line, index) => {
        line.style.cursor = 'pointer';
        line.addEventListener('click', () => {
            setAnchorLine(leftPanelId, rightPanelId, index);
        });
    });

    console.log('Line click handlers initialized');
}
