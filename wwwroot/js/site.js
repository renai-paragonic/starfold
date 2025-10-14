// Global variables for easter egg tracking
window.totalClicks = 0;
window.clickedTalmies = new Set();
window.alertShown = false;

document.addEventListener('DOMContentLoaded', () => {
    // Accordion functionality
    const accordionItems = document.querySelectorAll('.accordion-item');
    
    accordionItems.forEach(item => {
        const header = item.querySelector('.accordion-header');
        
        header.addEventListener('click', () => {
            const currentlyActive = document.querySelector('.accordion-item.active');
            
            if (currentlyActive && currentlyActive !== item) {
                currentlyActive.classList.remove('active');
            }
            
            item.classList.toggle('active');
        });
    });
    // Profile image press handling
    const profileContainer = document.querySelector('.profile-image-container');
    let timeoutId = null;

    function handleProfilePress() {
        // Clear any existing timeout
        if (timeoutId) {
            clearTimeout(timeoutId);
        }

        // Add the pressed class
        profileContainer.classList.add('pressed');

        // Set timeout to remove the pressed class after 2.5 seconds
        timeoutId = setTimeout(() => {
            profileContainer.classList.remove('pressed');
            timeoutId = null;
        }, 2500);
    }

    // Add event listeners for both click and touch
    profileContainer.addEventListener('mousedown', handleProfilePress);
    profileContainer.addEventListener('touchstart', (e) => {
        e.preventDefault(); // Prevent default touch behavior
        handleProfilePress();
    });

    // Talmy image jumping animation and easter egg tracking
    document.querySelectorAll('.talmy-image').forEach(img => {
        const handleTalmyClick = function(e) {
            // Prevent any default behavior
            if (e) {
                e.preventDefault();
                e.stopPropagation();
            }

            if (!this.classList.contains('jumping')) {
                this.classList.add('jumping');
                setTimeout(() => {
                    this.classList.remove('jumping');
                }, 800); // Match the animation duration

                // Easter egg tracking
                window.totalClicks++;
                window.clickedTalmies.add(this.classList.contains('talmy-left') ? 'left' : 'right');
                
                console.log('Click registered:', {
                    totalClicks: window.totalClicks,
                    clickedTalmies: Array.from(window.clickedTalmies),
                    isTouch: e && e.type.includes('touch')
                });

                // Check if easter egg conditions are met and alert hasn't been shown yet
                if (!window.alertShown && window.totalClicks >= 10 && window.clickedTalmies.size === 2) {
                    alert("You have unbound the sheep. What chaos you have wrought. You've played your stupid game. Email me at renaiparagonic@gmail.com or DM me on a shared platform for your stupid prize.");
                    window.alertShown = true;
                    
                    // Create and show the Talmy Overdrive container
                    const overdrive = document.createElement('div');
                    overdrive.className = 'talmy-overdrive';
                    
                    const gif = document.createElement('img');
                    gif.src = '/images/Talmy Overdrive.gif';
                    gif.alt = 'Talmy Overdrive';
                    
                    overdrive.appendChild(gif);
                    document.body.appendChild(overdrive);
                    
                    // Remove all other content
                    const content = document.querySelector('.content');
                    const desktopLayout = document.querySelector('.desktop-layout');
                    if (content) content.style.display = 'none';
                    if (desktopLayout) desktopLayout.style.display = 'none';
                }
            }
        };

        // Mouse events
        img.addEventListener('mousedown', handleTalmyClick);
        
        // Touch events for mobile - add multiple touch event listeners
        img.addEventListener('touchstart', function(e) {
            handleTalmyClick.call(this, e);
        }, { passive: false });
        
        // Prevent any unwanted touch behaviors
        img.addEventListener('touchend', function(e) {
            e.preventDefault();
        }, { passive: false });
        
        img.addEventListener('touchmove', function(e) {
            e.preventDefault();
        }, { passive: false });
    });
});
