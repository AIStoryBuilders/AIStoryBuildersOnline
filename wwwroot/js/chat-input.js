// Prevents the default newline insertion when the user presses Enter
// (without Shift) inside the Story Chat textarea, so that the Blazor
// keydown handler can cleanly send the message instead.
window.chatInput = {
    bindEnter: function (wrapperEl) {
        if (!wrapperEl) return;
        const textarea = wrapperEl.querySelector('textarea');
        if (!textarea || textarea.dataset.enterBound === '1') return;
        textarea.dataset.enterBound = '1';
        textarea.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
            }
        });
    }
};
