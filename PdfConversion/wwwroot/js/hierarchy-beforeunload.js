window.setupBeforeUnload = function(dotnetHelper) {
    console.log('[beforeunload] Setting up handler, dotnetHelper:', dotnetHelper);

    // Store warning state that can be checked synchronously
    let hasChanges = false;

    // Poll for changes state periodically
    const updateChangesState = async () => {
        try {
            hasChanges = await dotnetHelper.invokeMethodAsync('ShouldWarnBeforeUnload');
        } catch (error) {
            console.error('[beforeunload] Error polling changes state:', error);
        }
    };

    // Update immediately and then every 2 seconds
    updateChangesState();
    setInterval(updateChangesState, 2000);

    window.addEventListener('beforeunload', function(e) {
        console.log('[beforeunload] Event triggered, hasChanges:', hasChanges);
        if (hasChanges) {
            console.log('[beforeunload] Preventing unload due to unsaved changes');
            e.preventDefault();
            e.returnValue = '';
            return '';
        }
    });
};
