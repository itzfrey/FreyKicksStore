// Opens Google sign-in in a popup window instead of navigating the whole page.
// The popup ends on /account/login-success (see AccountController), which posts
// a message back to this window and closes itself; we listen for that message
// and reload the main page so it picks up the new signed-in state.
function openGoogleLoginPopup() {
    window.open(
        '/account/external-login?provider=Google&returnUrl=%2Faccount%2Flogin-success',
        'googleLogin',
        'width=500,height=650'
    );

    window.addEventListener('message', function handler(e) {
        if (e.origin !== window.location.origin) return;
        if (e.data === 'google-login-success') {
            window.removeEventListener('message', handler);
            window.location.reload();
        }
    });

    return false; // prevent the <a> tag's default navigation
}