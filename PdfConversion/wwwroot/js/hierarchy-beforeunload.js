window.setupBeforeUnload = function(dotnetHelper) {
    window.addEventListener('beforeunload', function(e) {
        if (dotnetHelper.invokeMethod('ShouldWarnBeforeUnload')) {
            e.preventDefault();
            e.returnValue = '';
            return '';
        }
    });
};
