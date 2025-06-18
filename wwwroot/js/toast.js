/**
 * Modern Toast Notification System
 * Premium, subtle animations with glassmorphism
 */

class ToastManager
{
    constructor()
    {
        this.container = null;
        this.toasts = new Map();
        this.init();
    }

    init()
    {
        // Create toast container if it doesn't exist
        this.container = document.querySelector('.toast-container');
        if (!this.container)
        {
            this.container = document.createElement('div');
            this.container.className = 'toast-container';
            document.body.appendChild(this.container);
        }
    }

    /**
     * Show a toast notification
     * @param {string} message - Toast message
     * @param {string} type - Toast type: 'success', 'error', 'warning', 'info'
     * @param {string} title - Optional title
     * @param {number} duration - Duration in ms (0 = manual close)
     * @param {object} options - Additional options
     */
    show(message, type = 'info', title = null, duration = 5000, options = { }) {
        const id = this.generateId();
    const toast = this.createToast(id, message, type, title, duration, options);
        
        this.toasts.set(id, toast);
        this.container.appendChild(toast.element);
        
        // Trigger entrance animation
        requestAnimationFrame(() => {
        toast.element.classList.add('fade-in');
    });

        // Auto-hide toast
        if (duration > 0) {
            toast.timer = setTimeout(() => {
        this.hide(id);
    }, duration);
        }

return id;
    }

    /**
     * Hide a specific toast
     * @param {string} id - Toast ID
     */
    hide(id) {
    const toast = this.toasts.get(id);
    if (!toast) return;

    // Clear timer
    if (toast.timer)
    {
        clearTimeout(toast.timer);
    }

    // Exit animation
    toast.element.style.transform = 'translateX(400px)';
    toast.element.style.opacity = '0';

    setTimeout(() => {
        if (toast.element.parentNode)
        {
            toast.element.parentNode.removeChild(toast.element);
        }
        this.toasts.delete(id);
    }, 300);
}

/**
 * Hide all toasts
 */
hideAll() {
    this.toasts.forEach((_, id) => this.hide(id));
}

/**
 * Create toast element
 */
createToast(id, message, type, title, duration, options) {
    const element = document.createElement('div');
    element.className = `toast - modern toast -${ type}`;
    element.setAttribute('data-toast-id', id);

    const icon = this.getIcon(type);
    const titleHtml = title ? `< div class= "toast-title" >${ this.escapeHtml(title)}</ div >` : '';

element.innerHTML = `
            < div class= "toast-icon" >
                ${ icon}
            </ div >
            < div class= "toast-content" >
                ${ titleHtml}
                < div class= "toast-message" >${ this.escapeHtml(message)}</ div >
            </ div >
            < button class= "toast-close" type = "button" >
                < svg width = "16" height = "16" viewBox = "0 0 24 24" fill = "currentColor" >
                    < path d = "M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z" />
                </ svg >
            </ button >
        `;

// Add close button event listener
const closeBtn = element.querySelector('.toast-close');
closeBtn.addEventListener('click', () => this.hide(id));

// Add click to dismiss (optional)
if (options.clickToDismiss !== false)
{
    element.addEventListener('click', (e) => {
        if (!closeBtn.contains(e.target))
        {
            this.hide(id);
        }
    });
}

// Add progress bar for timed toasts
if (duration > 0 && options.showProgress !== false)
{
    const progressBar = document.createElement('div');
    progressBar.className = 'toast-progress';
    progressBar.style.cssText = `
                position: absolute;
bottom: 0;
left: 0;
height: 3px;
background: currentColor;
width: 100 %;
opacity: 0.3;
animation: toastProgress ${ duration}
    ms linear;
            `;
    element.appendChild(progressBar);

    // Add progress animation
    const style = document.createElement('style');
    style.textContent = `
                @keyframes toastProgress {
        from { width: 100 %; }
        to { width: 0 %; }
    }
            `;
    document.head.appendChild(style);
}

return {
    element,
            timer: null
        }
;
    }

    /**
     * Get icon for toast type
     */
    getIcon(type) {
    const icons = {
            success: `< svg width = "24" height = "24" viewBox = "0 0 24 24" fill = "currentColor" style = "color: var(--accent-green)" >
                < path d = "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z" />
            </ svg >`,
            error: `< svg width = "24" height = "24" viewBox = "0 0 24 24" fill = "currentColor" style = "color: var(--accent-red)" >
                < path d = "M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z" />
            </ svg >`,
            warning: `< svg width = "24" height = "24" viewBox = "0 0 24 24" fill = "currentColor" style = "color: var(--accent-orange)" >
                < path d = "M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z" />
            </ svg >`,
            info: `< svg width = "24" height = "24" viewBox = "0 0 24 24" fill = "currentColor" style = "color: var(--accent-blue)" >
                < path d = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z" />
            </ svg >`
        }
;
return icons[type] || icons.info;
    }

    /**
     * Generate unique ID
     */
    generateId() {
    return 'toast_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
}

/**
 * Escape HTML to prevent XSS
 */
escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Convenience methods
success(message, title = 'Successo', duration = 4000) {
    return this.show(message, 'success', title, duration);
}

error(message, title = 'Errore', duration = 6000) {
    return this.show(message, 'error', title, duration);
}

warning(message, title = 'Attenzione', duration = 5000) {
    return this.show(message, 'warning', title, duration);
}

info(message, title = 'Informazione', duration = 4000) {
    return this.show(message, 'info', title, duration);
}

// Loading toast with spinner
loading(message, title = 'Caricamento...') {
    const id = this.generateId();
    const toast = this.createLoadingToast(id, message, title);

    this.toasts.set(id, toast);
    this.container.appendChild(toast.element);

    requestAnimationFrame(() => {
        toast.element.classList.add('fade-in');
    });

    return id;
}

createLoadingToast(id, message, title) {
    const element = document.createElement('div');
    element.className = 'toast-modern toast-info';
    element.setAttribute('data-toast-id', id);

    element.innerHTML = `
            < div class= "toast-icon" >
                < svg width = "24" height = "24" viewBox = "0 0 24 24" fill = "currentColor" style = "color: var(--accent-blue); animation: spin 1s linear infinite;" >
                    < path d = "M12,4a8,8 0 0,1 7.89,6.7 1.53,1.53 0 0,0 1.49,1.3 1.5,1.5 0 0,0 1.48-1.75 11,11 0 0,0-21.72,0A1.5,1.5 0 0,0 2.62,12a1.53,1.53 0 0,0 1.49-1.3A8,8 0 0,1 12,4Z" />
                </ svg >
            </ div >
            < div class= "toast-content" >
                < div class= "toast-title" >${ this.escapeHtml(title)}</ div >
                < div class= "toast-message" >${ this.escapeHtml(message)}</ div >
            </ div >
        `;

return { element, timer: null }
;
    }
}

// Create global toast instance
window.Toast = new ToastManager();

// jQuery plugin for backward compatibility
if (typeof jQuery !== 'undefined')
{
    jQuery.fn.toast = function(options) {
        const defaults = {
            message: '',
            type: 'info',
            title: null,
            duration: 5000
        }
    ;
    const settings = jQuery.extend({ }, defaults, options);
    return Toast.show(settings.message, settings.type, settings.title, settings.duration);
}
;
}

// Enhanced AJAX error handling with toasts
$(document).ready(function() {
    // Global AJAX error handler
    $(document).ajaxError(function(event, xhr, settings) {
        if (xhr.status === 0) return; // Ignore aborted requests

        let message = 'Si è verificato un errore di connessione';
        let title = 'Errore di Rete';

        if (xhr.responseJSON && xhr.responseJSON.message)
        {
            message = xhr.responseJSON.message;
        }
        else if (xhr.status === 404)
        {
            message = 'Risorsa non trovata';
            title = 'Errore 404';
        }
        else if (xhr.status === 500)
        {
            message = 'Errore interno del server';
            title = 'Errore 500';
        }
        else if (xhr.status === 403)
        {
            message = 'Accesso negato';
            title = 'Errore 403';
        }

        Toast.error(message, title);
    });

    // Global AJAX success handler for common operations
    $(document).ajaxSuccess(function(event, xhr, settings) {
        // Auto-show success toast for POST/PUT/DELETE operations
        if (['POST', 'PUT', 'DELETE'].includes(settings.type))
        {
            if (xhr.responseJSON && xhr.responseJSON.message && xhr.responseJSON.success)
            {
                Toast.success(xhr.responseJSON.message);
            }
        }
    });
});

// Export for module systems
if (typeof module !== 'undefined' && module.exports)
{
    module.exports = ToastManager;
}