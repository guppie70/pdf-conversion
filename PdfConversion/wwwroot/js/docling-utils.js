// Docling page utilities

/**
 * Scroll an element to the bottom (shows latest content)
 * @param {HTMLElement} element - The element to scroll
 */
window.scrollToBottom = function(element) {
    if (!element) {
        console.warn('scrollToBottom: element is null or undefined');
        return;
    }

    try {
        // Scroll to bottom with smooth behavior
        element.scrollTop = element.scrollHeight;
    } catch (error) {
        console.error('Error scrolling to bottom:', error);
    }
};

/**
 * Check if element is scrolled near the bottom (within threshold)
 * @param {HTMLElement} element - The element to check
 * @param {number} threshold - Distance from bottom in pixels (default 50)
 * @returns {boolean} True if near bottom
 */
window.isNearBottom = function(element, threshold = 50) {
    if (!element) return true;

    const scrollTop = element.scrollTop;
    const scrollHeight = element.scrollHeight;
    const clientHeight = element.clientHeight;

    return (scrollHeight - scrollTop - clientHeight) < threshold;
};

/**
 * Auto-scroll to bottom only if user hasn't manually scrolled up
 * @param {HTMLElement} element - The element to auto-scroll
 */
window.autoScrollToBottom = function(element) {
    if (!element) return;

    // Only auto-scroll if user is already at or near the bottom
    // This prevents annoying auto-scroll when user is reviewing older messages
    if (window.isNearBottom(element, 50)) {
        window.scrollToBottom(element);
    }
};
