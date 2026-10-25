(function () {
    const AUTO_PLAY_MS = 3000;

    function initCarousel() {
        const root = document.querySelector('[data-hero-carousel]');
        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        const slides = Array.from(root.querySelectorAll('.hero-slide'));
        if (slides.length <= 1) {
            root.dataset.initialized = 'true';
            return;
        }

        const reducedMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        if (reducedMotion) {
            root.dataset.initialized = 'true';
            return;
        }

        let activeIndex = 0;
        let timer = null;
        let touchStartX = null;

        function setActive(index) {
            activeIndex = (index + slides.length) % slides.length;
            slides.forEach((slide, idx) => {
                const isActive = idx === activeIndex;
                slide.classList.toggle('is-active', isActive);
                slide.setAttribute('aria-hidden', String(!isActive));
            });
            restartTimer();
        }

        function clearTimer() {
            if (timer) {
                clearInterval(timer);
                timer = null;
            }
        }

        function restartTimer() {
            clearTimer();
            timer = window.setInterval(() => {
                setActive(activeIndex + 1);
            }, AUTO_PLAY_MS);
        }

        root.addEventListener('pointerdown', (event) => {
            touchStartX = event.clientX;
            clearTimer();
        });

        root.addEventListener('pointerup', (event) => {
            if (touchStartX === null) {
                restartTimer();
                return;
            }

            const delta = event.clientX - touchStartX;
            touchStartX = null;

            if (Math.abs(delta) > 48) {
                setActive(activeIndex + (delta < 0 ? 1 : -1));
                return;
            }

            restartTimer();
        });

        root.addEventListener('mouseenter', () => clearTimer());
        root.addEventListener('mouseleave', () => restartTimer());
        root.addEventListener('focusin', () => clearTimer());
        root.addEventListener('focusout', () => restartTimer());

        root.dataset.initialized = 'true';
        restartTimer();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initCarousel, { once: true });
    } else {
        initCarousel();
    }

    document.addEventListener('blazor:enhancednav:load', () => {
        initCarousel();
    });
})();
