$(document).ready(function () {
    let currentBrowseButton = null;
    let selectedOpcNode = { nodeId: null };

    // --- Helper Functions ---
    function getAntiForgeryToken() {
        return window.antiForgeryToken;
    }

    function createMachineOptions() {
        if (!window.allMachines || window.allMachines.length === 0) {
            return '<option value="">Nessuna macchina</option>';
        }
        let options = '<option value="" selected disabled>Seleziona...</option>';
        window.allMachines.forEach(machine => {
            options += `<option value="${machine}">${machine}</option>`;
        });
        return options;
    }

    function createNewTagRow(param = {}) {
        const machineOptions = createMachineOptions();
        const nomeTag = param.nomeTag || '';
        const nomeMacchina = param.nomeMacchina || '';
        const connessione = param.connessione || '';
        const valore = param.valore || '';

        const newRow = `
            <tr class="parameter-row">
                <td><input type="text" class="form-control form-control-sm nome-tag" value="${nomeTag}" placeholder="Nome Tag"></td>
                <td><select class="form-select form-select-sm nome-macchina">${machineOptions}</select></td>
                <td>
                    <div class="input-group"><input type="text" class="form-control form-control-sm connessione-node" value="${connessione}" readonly placeholder="..."><button class="btn btn-outline-secondary btn-sm browse-node-btn" type="button"><i class="fas fa-sitemap"></i></button></div>
                </td>
                <td><input type="text" class="form-control form-control-sm valore" value="${valore}" placeholder="Valore"></td>
                <td><button class="btn btn-sm btn-outline-danger btn-delete-row" title="Rimuovi"><i class="fas fa-times"></i></button></td>
            </tr>`;

        const $newRow = $(newRow);
        if (nomeMacchina) {
            $newRow.find('.nome-macchina').val(nomeMacchina);
        }
        return $newRow;
    }

    // --- RECIPE CRUD ---
    $('#saveRecipeBtn').on('click', function () {
        const recipeName = $('#nomeRicetta').val().trim();
        if (!recipeName) {
            alert("Il nome della ricetta non può essere vuoto.");
            return;
        }

        $.ajax({
            url: window.ricetteUrls.addRicetta,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ NomeRicetta: recipeName }),
            headers: { 'RequestVerificationToken': getAntiForgeryToken() },
            success: function () {
                location.reload(); // Simple reload to show the new recipe
            },
            error: function (xhr) {
                alert('Errore: ' + (xhr.responseJSON ? xhr.responseJSON.message : 'Impossibile aggiungere la ricetta.'));
            }
        });
    });

    $('#updateRecipeBtn').on('click', function () {
        const originalName = $('#edit-original-recipe-name').val();
        const newName = $('#edit-recipe-name').val().trim();

        if (!newName) {
            alert("Il nuovo nome non può essere vuoto.");
            return;
        }

        $.ajax({
            url: window.ricetteUrls.editRicetta,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ OriginalNomeRicetta: originalName, NewNomeRicetta: newName }),
            headers: { 'RequestVerificationToken': getAntiForgeryToken() },
            success: function () {
                location.reload(); // Simple reload to reflect the change
            },
            error: function (xhr) {
                alert('Errore: ' + (xhr.responseJSON ? xhr.responseJSON.message : 'Impossibile modificare la ricetta.'));
            }
        });
    });

    $('#ricetteAccordion').on('click', '.btn-edit', function () {
        const originalName = $(this).data('recipe-name');
        $('#edit-original-recipe-name').val(originalName);
        $('#edit-recipe-name').val(originalName);
    });

    $('#ricetteAccordion').on('click', '.btn-delete', function () {
        const recipeName = $(this).data('recipe-name');
        if (confirm(`Sei sicuro di voler eliminare la ricetta "${recipeName}" e tutti i suoi parametri?`)) {
            $.ajax({
                url: window.ricetteUrls.deleteRicetta,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({ NomeRicetta: recipeName }),
                headers: { 'RequestVerificationToken': getAntiForgeryToken() },
                success: function () {
                    location.reload(); // Simple reload to remove the recipe
                },
                error: function (xhr) {
                    alert('Errore: ' + (xhr.responseJSON ? xhr.responseJSON.message : 'Impossibile eliminare la ricetta.'));
                }
            });
        }
    });

    // --- PARAMETER MANAGEMENT ---
    $('.accordion').on('click', '.btn-add-tag', function () {
        const $tableBody = $(this).closest('.card-body').find('tbody');
        $tableBody.find('.no-params-row').remove();
        const $newRow = createNewTagRow();
        $tableBody.append($newRow);
        $newRow.find('.nome-tag').focus();
    });

    $('.accordion').on('click', '.btn-delete-row', function () {
        $(this).closest('tr').fadeOut(300, function () { $(this).remove(); });
    });

    $('.accordion-collapse').on('show.bs.collapse', function () {
        const recipeName = $(this).closest('.accordion-item').data('recipe-name');
        const $tableBody = $(this).find('tbody');

        if ($tableBody.data('loaded')) return;

        $tableBody.html('<tr><td colspan="5" class="text-center"><div class="spinner-border spinner-border-sm"></div></td></tr>');

        $.ajax({
            url: `${window.ricetteUrls.getParametri}?nomeRicetta=${encodeURIComponent(recipeName)}`,
            type: 'GET',
            success: function (params) {
                $tableBody.empty();
                if (params && params.length > 0) {
                    params.forEach(param => $tableBody.append(createNewTagRow(param)));
                } else {
                    $tableBody.append('<tr class="no-params-row"><td colspan="5" class="text-center text-muted">Nessun parametro salvato.</td></tr>');
                }
                $tableBody.data('loaded', true);
            },
            error: function () {
                $tableBody.html('<tr><td colspan="5" class="text-center text-danger">Errore nel caricamento.</td></tr>');
            }
        });
    });

    $('.accordion').on('click', '.btn-save-recipe', function () {
        const $cardBody = $(this).closest('.card-body');
        const recipeName = $cardBody.closest('.accordion-item').data('recipe-name');
        const $button = $(this);
        let isValid = true;
        let parameters = [];

        $cardBody.find('tbody tr.parameter-row').each(function () {
            const $row = $(this);
            // CORREZIONE: Crea un oggetto che combacia esattamente con il modello C# ParametroRicetta
            const param = {
                NomeRicetta: recipeName, // Aggiunto il nome della ricetta
                NomeTag: $row.find('.nome-tag').val().trim(),
                NomeMacchina: $row.find('.nome-macchina').val(),
                Connessione: $row.find('.connessione-node').val().trim(),
                Valore: $row.find('.valore').val().trim()
            };

            // Basic validation
            if (!param.NomeTag || !param.NomeMacchina || !param.Connessione) {
                isValid = false;
                $row.find('input:not(.valore), select').not(':read-only').css('border-color', '#dc3545');
            } else {
                $row.find('input, select').css('border-color', '');
            }
            parameters.push(param);
        });

        if (!isValid) {
            alert('Compilare tutti i campi: Nome Tag, Nome Macchina, e Connessione.');
            return;
        }

        $button.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i>');

        $.ajax({
            url: window.ricetteUrls.salvaParametri,
            type: 'POST',
            contentType: 'application/json',
            // CORREZIONE: Il nome della proprietà "Parametri" ora combacia con il modello C# SalvaParametriModel
            data: JSON.stringify({ NomeRicetta: recipeName, Parametri: parameters }),
            headers: { 'RequestVerificationToken': getAntiForgeryToken() },
            success: (response) => alert(response.message),
            error: (xhr) => alert('Errore: ' + (xhr.responseJSON ? xhr.responseJSON.message : 'Errore di comunicazione.')),
            complete: () => $button.prop('disabled', false).html('<i class="fas fa-save me-2"></i>Salva Parametri')
        });
    });

    $('.accordion').on('click', '.btn-send-recipe', function () {
        const recipeName = $(this).closest('.accordion-item').data('recipe-name');
        const $button = $(this);

        if (!confirm(`Sei sicuro di voler inviare i valori per la ricetta "${recipeName}"?`)) return;

        $button.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i>');

        $.ajax({
            url: window.ricetteUrls.inviaRicetta,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ NomeRicetta: recipeName }),
            headers: { 'RequestVerificationToken': getAntiForgeryToken() },
            success: (response) => alert(response.message),
            error: (xhr) => alert('Errore: ' + (xhr.responseJSON ? xhr.responseJSON.message : 'Errore di comunicazione.')),
            complete: () => $button.prop('disabled', false).html('<i class="fas fa-paper-plane me-2"></i>Invia Ricetta')
        });
    });

    // --- OPC UA BROWSING ---
    $('.accordion').on('click', '.browse-node-btn', function () {
        const machineName = $(this).closest('tr').find('.nome-macchina').val();
        if (!machineName) {
            alert('Selezionare prima una macchina.');
            return;
        }

        currentBrowseButton = this;
        $('#opc-tree-container-modal').html('<div class="text-center"><div class="spinner-border"></div></div>');
        $('#browseOpcServerModal').modal('show');

        browseOpcNode(machineName, null);
    });

    $('#opc-tree-container-modal').on('click', '.opc-node', function (e) {
        e.stopPropagation();
        const $li = $(this).closest('li');
        const machineName = $('#browseOpcServerModal').data('machineName');
        const isVariable = $(this).hasClass('variable');

        if (isVariable) {
            $('#opc-tree-container-modal .bg-primary').removeClass('bg-primary text-white');
            $(this).addClass('bg-primary text-white');
            selectedOpcNode.nodeId = $li.data('node-id');
            $('#selectOpcNodeBtn').prop('disabled', false);
        } else {
            if ($li.children('ul').length > 0) {
                $li.children('ul').slideUp(200, function () { $(this).remove(); });
                $li.find('.opc-node-toggle').first().removeClass('fa-chevron-down').addClass('fa-chevron-right');
            } else {
                browseOpcNode(machineName, $li.data('node-id'), $li);
            }
        }
    });

    $('#selectOpcNodeBtn').on('click', function () {
        if (currentBrowseButton && selectedOpcNode.nodeId) {
            $(currentBrowseButton).closest('.input-group').find('.connessione-node').val(selectedOpcNode.nodeId);
        }
        $('#browseOpcServerModal').modal('hide');
    });

    $('#browseOpcServerModal').on('hidden.bs.modal', function () {
        currentBrowseButton = null;
        selectedOpcNode = {};
        $('#selectOpcNodeBtn').prop('disabled', true);
    });

    function browseOpcNode(machineName, nodeId, $element) {
        // CORREZIONE: Usa l'URL corretto per la navigazione, che è in OpcUaController
        const url = window.opcUaUrls.browse || '/OpcUa/Browse';
        $('#browseOpcServerModal').data('machineName', machineName);

        $.post(url, { nomeMacchina: machineName, nodeId: nodeId })
            .done(function (nodes) {
                if (nodes && nodes.length > 0) {
                    const $ul = $('<ul></ul>');
                    nodes.forEach(item => {
                        let icon = item.nodeClass === 'Object' ? 'fa-folder' : 'fa-tag';
                        let toggleIcon = item.hasChildren ? 'fa-chevron-right' : 'fa-grip-lines-vertical';

                        const $li = $(`<li data-node-id="${item.nodeId}"></li>`);
                        const $span = $(`<span class="opc-node ${item.nodeClass.toLowerCase()} d-block p-1"></span>`);
                        $span.append(`<i class="fas ${toggleIcon} opc-node-toggle text-muted"></i>`);
                        $span.append(`<i class="fas ${icon} text-warning"></i> `);
                        $span.append(`<span class="opc-node-name">${item.displayName}</span>`);
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
                    $('#opc-tree-container-modal').html('<div class="text-muted p-2">Nessun nodo trovato.</div>');
                }
            })
            .fail(() => $('#opc-tree-container-modal').html('<div class="alert alert-danger">Errore nel caricare la struttura del server.</div>'));
    }
});
