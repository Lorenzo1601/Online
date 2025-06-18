/**
 * Modern Recipe Management System
 * Enhanced with glassmorphism UI and premium animations
 */

class RecipeManager {
    constructor() {
        this.currentBrowseButton = null;
        this.selectedOpcNode = { nodeId: null };
        this.loadingToasts = new Map();
        this.init();
    }

    init() {
        this.bindEvents();
        this.initializeAnimations();
        this.setupAutoSave();
    }

    // Event binding
    bindEvents() {
        // Recipe CRUD operations
        this.bindRecipeCRUDEvents();

        // Parameter management
        this.bindParameterEvents();

        // OPC UA browsing
        this.bindOpcUaBrowsingEvents();

        // Form validations
        this.bindValidationEvents();
    }

    bindRecipeCRUDEvents() {
        console.log('Binding recipe CRUD events...');

        // Add recipe - FIXED
        $(document).on('click', '#saveRecipeBtn', (e) => {
            e.preventDefault();
            console.log('Save recipe button clicked');
            this.addRecipe();
        });

        // Edit recipe - FIXED
        $(document).on('click', '#updateRecipeBtn', (e) => {
            e.preventDefault();
            console.log('Update recipe button clicked');
            this.updateRecipe();
        });

        // Delete recipe - FIXED
        $(document).on('click', '.btn-delete', (e) => {
            e.preventDefault();
            console.log('Delete recipe button clicked');
            this.deleteRecipe(e);
        });

        // Edit button handler - FIXED
        $(document).on('click', '.btn-edit', (e) => {
            e.preventDefault();
            console.log('Edit recipe button clicked');
            this.openEditModal(e);
        });

        // Send recipe - FIXED
        $(document).on('click', '.btn-send-recipe', (e) => {
            e.preventDefault();
            console.log('Send recipe button clicked');
            this.sendRecipe(e);
        });
    }

    bindParameterEvents() {
        // Add parameter row
        $('.accordion').on('click', '.btn-add-tag', (e) => this.addParameterRow(e));

        // Delete parameter row
        $('.accordion').on('click', '.btn-delete-row', (e) => this.deleteParameterRow(e));

        // Save parameters
        $('.accordion').on('click', '.btn-save-recipe', (e) => this.saveParameters(e));

        // Load parameters when accordion opens
        $('.accordion-collapse').on('show.bs.collapse', (e) => this.loadParameters(e));
    }

    bindOpcUaBrowsingEvents() {
        console.log('Binding OPC UA browsing events...');

        // Browse node button - FIXED
        $(document).on('click', '.browse-node-btn', (e) => {
            e.preventDefault();
            console.log('Browse node button clicked');
            this.openOpcBrowser(e);
        });

        // OPC node selection - FIXED
        $(document).on('click', '#opc-tree-container-modal .opc-node', (e) => {
            e.preventDefault();
            e.stopPropagation();
            console.log('OPC node clicked');
            this.selectOpcNode(e);
        });

        // Confirm OPC node selection - FIXED
        $(document).on('click', '#selectOpcNodeBtn', (e) => {
            e.preventDefault();
            console.log('Confirm OPC node selection clicked');
            this.confirmOpcNodeSelection();
        });

        // Modal cleanup - FIXED
        $('#browseOpcServerModal').on('hidden.bs.modal', () => {
            console.log('OPC modal hidden - cleaning up');
            this.cleanupOpcModal();
        });
    }

    bindValidationEvents() {
        // Real-time validation for tag names
        $('.accordion').on('input', '.nome-tag', (e) => this.validateTagName(e.target));

        // Real-time validation for values
        $('.accordion').on('input', '.valore', (e) => this.validateValue(e.target));

        // Machine selection validation
        $('.accordion').on('change', '.nome-macchina', (e) => this.validateMachineSelection(e.target));
    }

    // Animation initialization
    initializeAnimations() {
        // Add smooth animations to accordion items
        $('.accordion-item').each((index, item) => {
            $(item).css('animation-delay', `${index * 0.1}s`);
            $(item).addClass('fade-in');
        });

        // Add hover effects to buttons
        this.initializeButtonAnimations();
    }

    initializeButtonAnimations() {
        // Enhanced button hover effects
        $(document).on('mouseenter', '.btn', function () {
            $(this).addClass('scale-in');
        }).on('mouseleave', '.btn', function () {
            $(this).removeClass('scale-in');
        });

        // Ripple effect for buttons
        $(document).on('click', '.btn', function (e) {
            const ripple = $('<span class="ripple"></span>');
            const rect = this.getBoundingClientRect();
            const size = Math.max(rect.width, rect.height);
            const x = e.clientX - rect.left - size / 2;
            const y = e.clientY - rect.top - size / 2;

            ripple.css({
                width: size,
                height: size,
                left: x,
                top: y,
                position: 'absolute',
                borderRadius: '50%',
                background: 'rgba(255, 255, 255, 0.3)',
                transform: 'scale(0)',
                animation: 'ripple 0.6s linear',
                pointerEvents: 'none'
            });

            $(this).css('position', 'relative').append(ripple);

            setTimeout(() => ripple.remove(), 600);
        });
    }

    // Auto-save functionality
    setupAutoSave() {
        let saveTimeout;

        $('.accordion').on('input', '.parameter-row input, .parameter-row select', () => {
            clearTimeout(saveTimeout);
            saveTimeout = setTimeout(() => {
                this.autoSaveIndicator();
            }, 2000);
        });
    }

    autoSaveIndicator() {
        const indicator = $('.auto-save-indicator');
        if (indicator.length === 0) {
            $('body').append(`
                <div class="auto-save-indicator">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M21,7L9,19L3.5,13.5L4.91,12.09L9,16.17L19.59,5.59L21,7Z"/>
                    </svg>
                    Bozza salvata automaticamente
                </div>
            `);

            setTimeout(() => {
                $('.auto-save-indicator').fadeOut(() => {
                    $('.auto-save-indicator').remove();
                });
            }, 3000);
        }
    }

    // Recipe CRUD methods
    async addRecipe() {
        const recipeName = $('#nomeRicetta').val().trim();

        if (!this.validateRecipeName(recipeName)) {
            return;
        }

        const loadingToast = Toast.loading('Creazione ricetta in corso...', 'Attendere...');

        try {
            const response = await this.makeRequest(window.ricetteUrls.addRicetta, {
                NomeRicetta: recipeName
            });

            Toast.hide(loadingToast);
            Toast.success('Ricetta creata con successo!', 'Operazione completata');

            // Add smooth transition before reload
            this.fadeOutAndReload();

        } catch (error) {
            Toast.hide(loadingToast);
            Toast.error(error.message || 'Errore durante la creazione della ricetta');
        }
    }

    async updateRecipe() {
        const originalName = $('#edit-original-recipe-name').val();
        const newName = $('#edit-recipe-name').val().trim();

        if (!this.validateRecipeName(newName)) {
            return;
        }

        const loadingToast = Toast.loading('Aggiornamento ricetta in corso...', 'Attendere...');

        try {
            const response = await this.makeRequest(window.ricetteUrls.editRicetta, {
                OriginalNomeRicetta: originalName,
                NewNomeRicetta: newName
            });

            Toast.hide(loadingToast);
            Toast.success('Ricetta aggiornata con successo!', 'Operazione completata');

            this.fadeOutAndReload();

        } catch (error) {
            Toast.hide(loadingToast);
            Toast.error(error.message || 'Errore durante l\'aggiornamento della ricetta');
        }
    }

    async deleteRecipe(event) {
        const recipeName = $(event.target).data('recipe-name');

        // Enhanced confirmation modal
        const confirmed = await this.showConfirmationModal(
            'Elimina Ricetta',
            `Sei sicuro di voler eliminare la ricetta "${recipeName}" e tutti i suoi parametri?`,
            'Questa operazione è irreversibile.',
            'danger'
        );

        if (!confirmed) return;

        const loadingToast = Toast.loading('Eliminazione ricetta in corso...', 'Attendere...');

        try {
            const response = await this.makeRequest(window.ricetteUrls.deleteRicetta, {
                NomeRicetta: recipeName
            });

            Toast.hide(loadingToast);
            Toast.success('Ricetta eliminata con successo!', 'Operazione completata');

            this.fadeOutAndReload();

        } catch (error) {
            Toast.hide(loadingToast);
            Toast.error(error.message || 'Errore durante l\'eliminazione della ricetta');
        }
    }

    openEditModal(event) {
        const originalName = $(event.target).data('recipe-name');
        $('#edit-original-recipe-name').val(originalName);
        $('#edit-recipe-name').val(originalName);

        // Add focus effect
        setTimeout(() => {
            $('#edit-recipe-name').focus().select();
        }, 300);
    }

    async sendRecipe(event) {
        const recipeName = $(event.target).closest('.accordion-item').data('recipe-name');

        const confirmed = await this.showConfirmationModal(
            'Invia Ricetta',
            `Sei sicuro di voler inviare i valori per la ricetta "${recipeName}"?`,
            'I valori verranno scritti sui server OPC UA configurati.',
            'warning'
        );

        if (!confirmed) return;

        const $button = $(event.target);
        this.setButtonLoading($button, true);

        try {
            const response = await this.makeRequest(window.ricetteUrls.inviaRicetta, {
                NomeRicetta: recipeName
            });

            Toast.success(response.message || 'Ricetta inviata con successo!');

        } catch (error) {
            Toast.error(error.message || 'Errore durante l\'invio della ricetta');
        } finally {
            this.setButtonLoading($button, false);
        }
    }

    // Parameter management methods
    addParameterRow(event) {
        const $tableBody = $(event.target).closest('.card-body').find('tbody');
        $tableBody.find('.no-params-row').remove();

        const $newRow = this.createNewTagRow();
        $tableBody.append($newRow);

        // Animate row appearance
        $newRow.hide().slideDown(300, () => {
            $newRow.find('.nome-tag').focus();
        });

        // Update row numbers
        this.updateRowNumbers($tableBody);
    }

    deleteParameterRow(event) {
        const $row = $(event.target).closest('tr');
        const $tableBody = $row.closest('tbody');

        $row.fadeOut(300, function () {
            $(this).remove();

            // Update row numbers
            this.updateRowNumbers($tableBody);

            // Show no params message if no rows left
            if ($tableBody.find('.parameter-row').length === 0) {
                $tableBody.append(`
                    <tr class="no-params-row fade-in">
                        <td colspan="5" class="text-center text-muted py-4">
                            <svg width="32" height="32" viewBox="0 0 24 24" fill="currentColor" class="mb-2 opacity-50">
                                <path d="M19,13H13V19H11V13H5V11H11V5H13V11H19V13Z"/>
                            </svg>
                            <div>Nessun parametro configurato</div>
                            <small>Clicca "Aggiungi Tag" per iniziare</small>
                        </td>
                    </tr>
                `);
            }
        }.bind(this));
    }

    async loadParameters(event) {
        const $accordion = $(event.target);
        const recipeName = $accordion.closest('.accordion-item').data('recipe-name');
        const $tableBody = $accordion.find('tbody');

        if ($tableBody.data('loaded')) return;

        // Show loading skeleton
        this.showLoadingSkeleton($tableBody);

        try {
            const response = await fetch(
                `${window.ricetteUrls.getParametri}?nomeRicetta=${encodeURIComponent(recipeName)}`
            );

            if (!response.ok) throw new Error('Failed to load parameters');

            const params = await response.json();

            $tableBody.empty();

            if (params && params.length > 0) {
                params.forEach((param, index) => {
                    const $row = this.createNewTagRow(param);
                    $row.css('animation-delay', `${index * 0.1}s`);
                    $row.addClass('slide-up');
                    $tableBody.append($row);
                });
            } else {
                $tableBody.append(`
                    <tr class="no-params-row fade-in">
                        <td colspan="5" class="text-center text-muted py-4">
                            <svg width="32" height="32" viewBox="0 0 24 24" fill="currentColor" class="mb-2 opacity-50">
                                <path d="M19,13H13V19H11V13H5V11H11V5H13V11H19V13Z"/>
                            </svg>
                            <div>Nessun parametro salvato</div>
                            <small>Clicca "Aggiungi Tag" per iniziare</small>
                        </td>
                    </tr>
                `);
            }

            $tableBody.data('loaded', true);

        } catch (error) {
            $tableBody.html(`
                <tr>
                    <td colspan="5" class="text-center text-danger py-4">
                        <svg width="32" height="32" viewBox="0 0 24 24" fill="currentColor" class="mb-2">
                            <path d="M19,6.41L17.59,5 12,10.59 6.41,5 5,6.41 10.59,12 5,17.59 6.41,19 12,13.41 17.59,19 19,17.59 13.41,12z"/>
                        </svg>
                        <div>Errore nel caricamento dei parametri</div>
                        <small>Riprova più tardi</small>
                    </td>
                </tr>
            `);
        }
    }

    async saveParameters(event) {
        const $cardBody = $(event.target).closest('.card-body');
        const recipeName = $cardBody.closest('.accordion-item').data('recipe-name');
        const $button = $(event.target);

        // Validate all parameters
        const { isValid, parameters } = this.validateAndCollectParameters($cardBody, recipeName);

        if (!isValid) {
            Toast.error('Completa tutti i campi obbligatori prima di salvare');
            return;
        }

        this.setButtonLoading($button, true);

        try {
            const response = await this.makeRequest(window.ricetteUrls.salvaParametri, {
                NomeRicetta: recipeName,
                Parametri: parameters
            });

            Toast.success(response.message || 'Parametri salvati con successo!');

            // Add success animation to rows
            $cardBody.find('.parameter-row').each((index, row) => {
                setTimeout(() => {
                    $(row).addClass('glow-green');
                    setTimeout(() => $(row).removeClass('glow-green'), 1000);
                }, index * 100);
            });

        } catch (error) {
            Toast.error(error.message || 'Errore durante il salvataggio dei parametri');
        } finally {
            this.setButtonLoading($button, false);
        }
    }

    // OPC UA browsing methods
    openOpcBrowser(event) {
        const machineName = $(event.target).closest('tr').find('.nome-macchina').val();

        if (!machineName) {
            Toast.warning('Seleziona prima una macchina per navigare il server OPC UA');
            return;
        }

        this.currentBrowseButton = event.target;
        $('#opc-tree-container-modal').html(this.createLoadingSpinner('Caricamento struttura server...'));
        $('#browseOpcServerModal').modal('show');

        this.browseOpcNode(machineName, null);
    }

    selectOpcNode(event) {
        event.stopPropagation();
        console.log('Selecting OPC node...');

        const $clickedElement = $(event.target);
        const $li = $clickedElement.closest('li');
        const machineName = $('#browseOpcServerModal').data('machineName');
        const isVariable = $clickedElement.hasClass('variable') || $clickedElement.closest('.opc-node').hasClass('variable');

        console.log('Node details:', {
            nodeId: $li.data('node-id'),
            isVariable: isVariable,
            hasChildren: $li.find('.fa-chevron-right').length > 0
        });

        if (isVariable) {
            // Clear previous selections
            $('#opc-tree-container-modal .selected-node').removeClass('selected-node');
            $clickedElement.closest('.opc-node').addClass('selected-node');

            this.selectedOpcNode.nodeId = $li.data('node-id');
            $('#selectOpcNodeBtn').prop('disabled', false);

            console.log('Variable node selected:', this.selectedOpcNode.nodeId);

            // Add visual feedback
            $clickedElement.closest('.opc-node').css({
                'background-color': 'rgba(59, 130, 246, 0.3)',
                'border': '2px solid #3b82f6',
                'border-radius': '0.5rem'
            });

            Toast.info(`Nodo selezionato: ${$li.data('node-id')}`, 'Selezione OPC UA');

        } else {
            // Toggle folder
            console.log('Toggling folder node...');
            if ($li.children('ul').length > 0) {
                $li.children('ul').slideUp(200, function () {
                    $(this).remove();
                });
                $li.find('.opc-node-toggle').first().removeClass('fa-chevron-down').addClass('fa-chevron-right');
            } else {
                this.browseOpcNode(machineName, $li.data('node-id'), $li);
            }
        }
    }

    confirmOpcNodeSelection() {
        console.log('Confirming OPC node selection...', this.selectedOpcNode);

        if (this.currentBrowseButton && this.selectedOpcNode.nodeId) {
            const $input = $(this.currentBrowseButton).closest('.input-group').find('.connessione-node');
            $input.val(this.selectedOpcNode.nodeId);

            console.log('Node ID set in input:', this.selectedOpcNode.nodeId);

            // Add success visual feedback
            $input.css({
                'background-color': 'rgba(34, 197, 94, 0.2)',
                'border-color': '#22c55e',
                'color': '#ffffff'
            });

            setTimeout(() => {
                $input.css({
                    'background-color': '',
                    'border-color': '',
                    'color': ''
                });
            }, 2000);

            Toast.success('Nodo OPC UA selezionato con successo', 'Configurazione aggiornata');
        } else {
            console.warn('Cannot confirm selection - missing button or node ID');
            Toast.error('Errore nella selezione del nodo');
        }

        $('#browseOpcServerModal').modal('hide');
    }

    cleanupOpcModal() {
        this.currentBrowseButton = null;
        this.selectedOpcNode = {};
        $('#selectOpcNodeBtn').prop('disabled', true);
    }

    async browseOpcNode(machineName, nodeId, $element) {
        const url = window.opcUaUrls.browse || '/OpcUa/Browse';
        $('#browseOpcServerModal').data('machineName', machineName);

        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                },
                body: new URLSearchParams({
                    nomeMacchina: machineName,
                    nodeId: nodeId || ''
                })
            });

            if (!response.ok) throw new Error('Browse request failed');

            const nodes = await response.json();

            if (nodes && nodes.length > 0) {
                const $ul = $('<ul></ul>');

                nodes.forEach(item => {
                    const icon = item.nodeClass === 'Object' ? 'fa-folder' : 'fa-tag';
                    const toggleIcon = item.hasChildren ? 'fa-chevron-right' : 'fa-grip-lines-vertical';
                    const nodeClass = item.nodeClass.toLowerCase();

                    const $li = $(`<li data-node-id="${item.nodeId}"></li>`);
                    const $span = $(`<span class="opc-node ${nodeClass} d-block p-2 rounded"></span>`);

                    $span.html(`
                        <i class="fas ${toggleIcon} opc-node-toggle text-muted me-2"></i>
                        <i class="fas ${icon} text-warning me-2"></i>
                        <span class="opc-node-name">${item.displayName}</span>
                        ${item.nodeClass === 'Variable' ? '<span class="badge bg-success ms-2">Variable</span>' : ''}
                    `);

                    // Make variables selectable
                    if (item.nodeClass === 'Variable') {
                        $span.addClass('variable');
                        $span.css({
                            'cursor': 'pointer',
                            'border': '1px solid rgba(59, 130, 246, 0.3)',
                            'transition': 'all 0.2s ease'
                        });

                        $span.hover(
                            function () {
                                $(this).css('background-color', 'rgba(59, 130, 246, 0.1)');
                            },
                            function () {
                                if (!$(this).hasClass('selected-node')) {
                                    $(this).css('background-color', '');
                                }
                            }
                        );
                    }

                    $li.append($span);
                    $ul.append($li);
                });

                if ($element) {
                    $element.append($ul.hide().slideDown(200));
                    $element.find('.opc-node-toggle').first().removeClass('fa-chevron-right').addClass('fa-chevron-down');
                } else {
                    $('#opc-tree-container-modal').html($ul);
                }
            } else if (!$element) {
                $('#opc-tree-container-modal').html(`
                    <div class="text-center text-muted p-4">
                        <svg width="48" height="48" viewBox="0 0 24 24" fill="currentColor" class="mb-3 opacity-50">
                            <path d="M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4Z"/>
                        </svg>
                        <div>Nessun nodo trovato</div>
                        <small>Il server potrebbe essere vuoto o non accessibile</small>
                    </div>
                `);
            }
        } catch (error) {
            const errorHtml = `
                <div class="alert alert-danger text-center">
                    <svg width="32" height="32" viewBox="0 0 24 24" fill="currentColor" class="mb-2">
                        <path d="M19,6.41L17.59,5 12,10.59 6.41,5 5,6.41 10.59,12 5,17.59 6.41,19 12,13.41 17.59,19 19,17.59 13.41,12z"/>
                    </svg>
                    <div>Errore nel caricare la struttura del server</div>
                    <small>${error.message}</small>
                </div>
            `;
            $('#opc-tree-container-modal').html(errorHtml);
        }
    }

    // Utility methods
    createNewTagRow(param = {}) {
        const machineOptions = this.createMachineOptions();
        const nomeTag = this.escapeHtml(param.nomeTag || '');
        const nomeMacchina = this.escapeHtml(param.nomeMacchina || '');
        const connessione = this.escapeHtml(param.connessione || '');
        const valore = this.escapeHtml(param.valore || '');

        const newRow = `
            <tr class="parameter-row">
                <td>
                    <div class="input-group">
                        <span class="input-group-text">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
                                <path d="M5.5,7A1.5,1.5 0 0,1 4,5.5A1.5,1.5 0 0,1 5.5,4A1.5,1.5 0 0,1 7,5.5A1.5,1.5 0 0,1 5.5,7M21.41,11.58L12.41,2.58C12.05,2.22 11.55,2 11,2H4C2.89,2 2,2.89 2,4V11C2,11.55 2.22,12.05 2.59,12.41L11.58,21.41C11.95,21.78 12.45,22 13,22C13.55,22 14.05,21.78 14.41,21.41L21.41,14.41C21.78,14.05 22,13.55 22,13C22,12.45 21.78,11.95 21.41,11.58Z"/>
                            </svg>
                        </span>
                        <input type="text" class="form-control nome-tag" value="${nomeTag}" placeholder="Nome Tag" />
                    </div>
                </td>
                <td>
                    <select class="form-select nome-macchina">${machineOptions}</select>
                </td>
                <td>
                    <div class="input-group">
                        <input type="text" class="form-control connessione-node" value="${connessione}" readonly placeholder="Seleziona nodo..." />
                        <button class="btn btn-outline-secondary browse-node-btn" type="button" title="Naviga Server OPC UA">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
                                <path d="M12,2A2,2 0 0,1 14,4A2,2 0 0,1 12,6A2,2 0 0,1 10,4A2,2 0 0,1 12,2M21,9V7L15,1H5C3.89,1 3,1.89 3,3V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V9M19,19H5V3H13V9H19V19Z"/>
                            </svg>
                        </button>
                    </div>
                </td>
                <td>
                    <div class="input-group">
                        <span class="input-group-text">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
                                <path d="M9,7H11V17H9V7M12,7H14V17H12V7M15,7H17V17H15V7M3,5V7H5V19A2,2 0 0,0 7,21H17A2,2 0 0,0 19,19V7H21V5H3Z"/>
                            </svg>
                        </span>
                        <input type="text" class="form-control valore" value="${valore}" placeholder="Valore" />
                    </div>
                </td>
                <td class="text-center">
                    <button class="btn btn-sm btn-outline-danger btn-delete-row" title="Rimuovi parametro">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
                            <path d="M19,4H15.5L14.5,3H9.5L8.5,4H5V6H19M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19Z"/>
                        </svg>
                    </button>
                </td>
            </tr>
        `;

        const $newRow = $(newRow);
        if (nomeMacchina) {
            $newRow.find('.nome-macchina').val(nomeMacchina);
        }
        return $newRow;
    }

    createMachineOptions() {
        if (!window.allMachines || window.allMachines.length === 0) {
            return '<option value="">Nessuna macchina disponibile</option>';
        }

        let options = '<option value="" selected disabled>Seleziona macchina...</option>';
        window.allMachines.forEach(machine => {
            options += `<option value="${this.escapeHtml(machine)}">${this.escapeHtml(machine)}</option>`;
        });
        return options;
    }

    validateAndCollectParameters($cardBody, recipeName) {
        let isValid = true;
        const parameters = [];

        $cardBody.find('tbody tr.parameter-row').each((index, row) => {
            const $row = $(row);
            const param = {
                NomeRicetta: recipeName,
                NomeTag: $row.find('.nome-tag').val().trim(),
                NomeMacchina: $row.find('.nome-macchina').val(),
                Connessione: $row.find('.connessione-node').val().trim(),
                Valore: $row.find('.valore').val().trim()
            };

            // Validation
            if (!param.NomeTag || !param.NomeMacchina || !param.Connessione) {
                isValid = false;
                $row.find('input:not(.valore), select').not('[readonly]').each((i, input) => {
                    if (!$(input).val()) {
                        $(input).addClass('is-invalid');
                        setTimeout(() => $(input).removeClass('is-invalid'), 3000);
                    }
                });
            } else {
                $row.find('input, select').removeClass('is-invalid');
            }

            parameters.push(param);
        });

        return { isValid, parameters };
    }

    // Validation methods
    validateRecipeName(name) {
        if (!name) {
            Toast.error('Il nome della ricetta non può essere vuoto');
            return false;
        }

        if (name.length < 2) {
            Toast.error('Il nome della ricetta deve essere di almeno 2 caratteri');
            return false;
        }

        if (name.length > 100) {
            Toast.error('Il nome della ricetta non può superare i 100 caratteri');
            return false;
        }

        return true;
    }

    validateTagName(input) {
        const value = input.value.trim();
        const $input = $(input);

        if (value.length > 0 && value.length < 2) {
            $input.addClass('is-invalid');
            this.showInputError($input, 'Il nome del tag deve essere di almeno 2 caratteri');
        } else {
            $input.removeClass('is-invalid');
            this.hideInputError($input);
        }
    }

    validateValue(input) {
        const value = input.value.trim();
        const $input = $(input);

        // Basic numeric validation (can be enhanced based on requirements)
        if (value && isNaN(value) && !/^[a-zA-Z]+$/.test(value)) {
            $input.addClass('is-warning');
            this.showInputWarning($input, 'Verifica che il valore sia corretto');
        } else {
            $input.removeClass('is-warning');
            this.hideInputWarning($input);
        }
    }

    validateMachineSelection(select) {
        const $select = $(select);
        const value = $select.val();

        if (!value) {
            $select.addClass('is-invalid');
        } else {
            $select.removeClass('is-invalid');

            // Clear connection field when machine changes
            const $connectionInput = $select.closest('tr').find('.connessione-node');
            if ($connectionInput.val()) {
                $connectionInput.val('').addClass('glow-red');
                setTimeout(() => $connectionInput.removeClass('glow-red'), 1000);
                Toast.info('Riseleziona il nodo OPC UA per la nuova macchina');
            }
        }
    }

    // UI helper methods
    setButtonLoading($button, isLoading) {
        if (isLoading) {
            const originalHtml = $button.html();
            $button.data('original-html', originalHtml);
            $button.prop('disabled', true);
            $button.html(`
                <div class="spinner-border spinner-border-sm me-2" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
                Attendere...
            `);
        } else {
            $button.prop('disabled', false);
            $button.html($button.data('original-html'));
        }
    }

    showLoadingSkeleton($container) {
        const skeletonHtml = Array(3).fill().map((_, index) => `
            <tr class="skeleton-row">
                <td><div class="skeleton-item" style="animation-delay: ${index * 0.1}s"></div></td>
                <td><div class="skeleton-item" style="animation-delay: ${index * 0.1 + 0.05}s"></div></td>
                <td><div class="skeleton-item" style="animation-delay: ${index * 0.1 + 0.1}s"></div></td>
                <td><div class="skeleton-item" style="animation-delay: ${index * 0.1 + 0.15}s"></div></td>
                <td><div class="skeleton-item" style="animation-delay: ${index * 0.1 + 0.2}s"></div></td>
            </tr>
        `).join('');

        $container.html(skeletonHtml);
    }

    createLoadingSpinner(message = 'Caricamento...') {
        return `
            <div class="text-center p-4">
                <div class="spinner-border text-primary mb-3" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
                <div class="fw-medium">${message}</div>
            </div>
        `;
    }

    updateRowNumbers($tableBody) {
        $tableBody.find('.parameter-row').each((index, row) => {
            $(row).find('.row-number').text(index + 1);
        });
    }

    showInputError($input, message) {
        this.hideInputError($input);
        const errorId = `error-${Date.now()}`;
        $input.after(`<div class="invalid-feedback d-block" id="${errorId}">${message}</div>`);
        setTimeout(() => $(`#${errorId}`).remove(), 3000);
    }

    hideInputError($input) {
        $input.next('.invalid-feedback').remove();
    }

    showInputWarning($input, message) {
        this.hideInputWarning($input);
        const warningId = `warning-${Date.now()}`;
        $input.after(`<div class="text-warning small mt-1" id="${warningId}">${message}</div>`);
        setTimeout(() => $(`#${warningId}`).remove(), 3000);
    }

    hideInputWarning($input) {
        $input.next('.text-warning').remove();
    }

    async showConfirmationModal(title, message, description, type = 'warning') {
        return new Promise((resolve) => {
            const modalId = `confirmModal-${Date.now()}`;
            const iconMap = {
                warning: 'M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z',
                danger: 'M19,4H15.5L14.5,3H9.5L8.5,4H5V6H19M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19Z',
                info: 'M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4M11,16.5L6.5,12L7.91,10.59L11,13.67L16.59,8.09L18,9.5L11,16.5Z'
            };

            const modalHtml = `
                <div class="modal fade" id="${modalId}" tabindex="-1">
                    <div class="modal-dialog modal-dialog-centered">
                        <div class="modal-content">
                            <div class="modal-header">
                                <h5 class="modal-title">
                                    <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor" class="me-2">
                                        <path d="${iconMap[type]}"/>
                                    </svg>
                                    ${title}
                                </h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                            </div>
                            <div class="modal-body">
                                <p class="mb-2">${message}</p>
                                ${description ? `<small class="text-muted">${description}</small>` : ''}
                            </div>
                            <div class="modal-footer">
                                <button type="button" class="btn btn-outline" data-bs-dismiss="modal">Annulla</button>
                                <button type="button" class="btn btn-${type === 'danger' ? 'danger' : 'warning'}" id="confirmBtn">Conferma</button>
                            </div>
                        </div>
                    </div>
                </div>
            `;

            $('body').append(modalHtml);
            const $modal = $(`#${modalId}`);
            const modal = new bootstrap.Modal($modal[0]);

            $modal.find('#confirmBtn').on('click', () => {
                modal.hide();
                resolve(true);
            });

            $modal.on('hidden.bs.modal', () => {
                $modal.remove();
                resolve(false);
            });

            modal.show();
        });
    }

    fadeOutAndReload() {
        $('body').fadeOut(300, () => {
            location.reload();
        });
    }

    async makeRequest(url, data) {
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': window.antiForgeryToken
            },
            body: JSON.stringify(data)
        });

        const result = await response.json();

        if (!response.ok) {
            throw new Error(result.message || `HTTP ${response.status}`);
        }

        if (result.success === false) {
            throw new Error(result.message || 'Operation failed');
        }

        return result;
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

// Initialize when document is ready
$(document).ready(() => {
    window.recipeManager = new RecipeManager();

    // Add custom CSS for animations
    const customCSS = `
        <style>
        .ripple {
            position: absolute;
            border-radius: 50%;
            background: rgba(255, 255, 255, 0.3);
            transform: scale(0);
            animation: ripple 0.6s linear;
            pointer-events: none;
        }
        
        @keyframes ripple {
            to {
                transform: scale(4);
                opacity: 0;
            }
        }
        
        .skeleton-item {
            height: 20px;
            background: linear-gradient(90deg, var(--primary-600) 25%, var(--primary-500) 50%, var(--primary-600) 75%);
            background-size: 200% 100%;
            animation: skeleton-loading 1.5s infinite;
            border-radius: var(--radius-sm);
        }
        
        @keyframes skeleton-loading {
            0% { background-position: 200% 0; }
            100% { background-position: -200% 0; }
        }
        
        .auto-save-indicator {
            position: fixed;
            bottom: 20px;
            right: 20px;
            background: var(--glass-bg);
            backdrop-filter: blur(20px);
            border: 1px solid var(--glass-border);
            border-radius: var(--radius-lg);
            padding: var(--space-3) var(--space-4);
            color: var(--accent-green);
            display: flex;
            align-items: center;
            gap: var(--space-2);
            z-index: 9998;
            animation: slideUp 0.3s ease-out;
            box-shadow: var(--glass-shadow);
        }
        
        .selected-node {
            background: rgba(59, 130, 246, 0.2) !important;
            border: 1px solid var(--accent-blue) !important;
        }
        
        .is-warning {
            border-color: var(--accent-orange) !important;
            background-color: var(--accent-orange-light) !important;
        }
        </style>
    `;

    $('head').append(customCSS);
});