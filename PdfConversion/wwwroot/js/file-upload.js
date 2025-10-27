// File Upload Utilities for DoclingConvert page

/**
 * Handle file drop event by triggering the hidden InputFile component
 * @param {DragEvent} dropEvent - The drop event from the browser
 * @param {DotNetObject} dotNetHelper - .NET object reference for callbacks
 */
window.handleFileDrop = async function(dropEvent, dotNetHelper) {
    try {
        const files = dropEvent.dataTransfer.files;

        if (files.length === 0) {
            console.warn('No files dropped');
            return;
        }

        // Take only the first file
        const droppedFile = files[0];

        // Validate file type
        const allowedTypes = ['application/pdf', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 'application/msword'];
        const allowedExtensions = ['.pdf', '.docx', '.doc'];
        const fileExtension = droppedFile.name.substring(droppedFile.name.lastIndexOf('.')).toLowerCase();

        if (!allowedTypes.includes(droppedFile.type) && !allowedExtensions.includes(fileExtension)) {
            console.error('Invalid file type:', droppedFile.type, droppedFile.name);
            await dotNetHelper.invokeMethodAsync('OnFileDropError',
                `Invalid file type. Please upload PDF or Word documents only.`);
            return;
        }

        // Check file size (50MB limit)
        const maxSize = 50 * 1024 * 1024; // 50MB
        if (droppedFile.size > maxSize) {
            console.error('File too large:', droppedFile.size);
            await dotNetHelper.invokeMethodAsync('OnFileDropError',
                `File is too large. Maximum size is 50MB.`);
            return;
        }

        console.log('Processing dropped file:', droppedFile.name, droppedFile.type, droppedFile.size);

        // Find the hidden file input element
        const fileInput = document.getElementById('fileInput');

        if (!fileInput) {
            console.error('File input element not found');
            await dotNetHelper.invokeMethodAsync('OnFileDropError',
                'Upload area not properly initialized. Please refresh the page.');
            return;
        }

        // Create a DataTransfer object to set files on the input
        const dataTransfer = new DataTransfer();
        dataTransfer.items.add(droppedFile);

        // Set the files on the input element
        fileInput.files = dataTransfer.files;

        // Trigger the change event to notify Blazor
        const changeEvent = new Event('change', { bubbles: true });
        fileInput.dispatchEvent(changeEvent);

        console.log('File drop handled - triggered InputFile component');

    } catch (error) {
        console.error('Error in handleFileDrop:', error);
        if (dotNetHelper) {
            await dotNetHelper.invokeMethodAsync('OnFileDropError',
                `Unexpected error: ${error.message}`);
        }
    }
};

/**
 * Initialize drag and drop listeners on an element
 * @param {HTMLElement} element - The drop zone element
 * @param {DotNetObject} dotNetHelper - .NET object reference for callbacks
 */
window.initializeFileDrop = function(element, dotNetHelper) {
    if (!element) {
        console.error('Drop zone element not found');
        return;
    }

    console.log('Initializing file drop on element:', element);

    // Store the dotNetHelper reference on the element
    element._dotNetHelper = dotNetHelper;

    // Add dragenter event listener for visual feedback
    element.addEventListener('dragenter', function(e) {
        e.preventDefault();
        e.stopPropagation();
        element.classList.add('dragging');
        console.log('Drag enter detected');
    });

    // Add dragleave event listener for visual feedback
    element.addEventListener('dragleave', function(e) {
        e.preventDefault();
        e.stopPropagation();
        // Only remove dragging class if we're leaving the drop zone entirely
        if (e.target === element) {
            element.classList.remove('dragging');
            console.log('Drag leave detected');
        }
    });

    // Add dragover event listener (required for drop to work)
    element.addEventListener('dragover', function(e) {
        e.preventDefault();
        e.stopPropagation();
        e.dataTransfer.dropEffect = 'copy'; // Show copy cursor
    });

    // Add drop event listener
    element.addEventListener('drop', async function(e) {
        e.preventDefault();
        e.stopPropagation();

        console.log('Drop event detected');
        element.classList.remove('dragging'); // Remove visual feedback

        if (element._dotNetHelper) {
            await window.handleFileDrop(e, element._dotNetHelper);
        }
    });

    console.log('File drop initialized successfully');
};

/**
 * Clean up drag and drop listeners
 * @param {HTMLElement} element - The drop zone element
 */
window.disposeFileDrop = function(element) {
    if (element && element._dotNetHelper) {
        element._dotNetHelper = null;
    }
};
