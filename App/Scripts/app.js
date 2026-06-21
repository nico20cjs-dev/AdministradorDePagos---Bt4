var convertContenedor = function (contenedor) {
    var objContenedor = null;

    if (contenedor == undefined || contenedor == '') {
        objContenedor = $('#contentMensajes');
    } else {
        objContenedor = $(contenedor);
    }

    return objContenedor;
};

var procesarMensajes = function (lstMsj, contenedor) {

    var objContenedor = convertContenedor(contenedor);

    lstMsj.forEach(function (msj) {

        let classColor = 'info';
        let title = 'Informacion';
        switch (msj.Tipo) {
            case 0:
                classColor = 'error';
                title = 'Error';
                break;
            case 1:
                classColor = 'info';
                title = 'Informacion';
                break;
            case 2:
                classColor = 'warning';
                title = 'Advertencia';
                break;
            case 3:
                classColor = 'success';
                title = 'Exito';
                break;
            case 4:
                classColor = 'warning';
                title = 'Advertencia';
                break;
            default:
                break;
        }

        let panelMsj =
            `<article class="card-msj msg-card msg-` + classColor + `">
                <h4>` + title + `</h4>
                <p>` + msj.Texto + `</p>
            </article>`;

        objContenedor.append(panelMsj);

    });
};

var limpiarMensajes = function (contenedor) {
    var objContenedor = convertContenedor(contenedor);
    objContenedor.children('.card-msj').remove()
};

var table = null;

function parseMvcDate(value) {
    if (!value) return '';
    var ticks = value.toString().replace('/Date(', '').replace(')/', '');
    var date = new Date(parseInt(ticks, 10));
    if (isNaN(date.getTime())) return '';
    var mm = date.getMonth() + 1;
    var dd = date.getDate();
    return date.getFullYear() + '-' + (mm > 9 ? '' : '0') + mm + '-' + (dd > 9 ? '' : '0') + dd;
}

function formatArs(value) {
    return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(value || 0);
}

function formatUsd(value) {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', minimumFractionDigits: 2 }).format(value || 0);
}

function setLoading(isLoading) {
    var button = $('#btnActualizarPdf');
    var state = $('#loadState');
    var spinner = $('#loadSpinner');
    if (isLoading) {
        button.prop('disabled', true).text('Actualizando...');
        state.text('Procesando comprobantes...');
        spinner.addClass('is-active').attr('aria-hidden', 'false');
    } else {
        button.prop('disabled', false).attr('title', 'Actualizar la lista de pagos desde los comprobantes PDF').html('<svg class="btn-icon" viewBox="0 0 24 24" width="16" height="16" aria-hidden="true"><path fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" d="M1 4v6h6m16 10v-6h-6M3.51 15a9 9 0 0 0 14.85 3.36l-2.12-2.12a5.5 5.5 0 0 1-8.37-.74M20.49 9a9 9 0 0 0-14.85-3.36l2.12 2.12a5.5 5.5 0 0 1 8.37.74"/></svg><span>Actualizar pagos</span>');
        state.text('Listo');
        spinner.removeClass('is-active').attr('aria-hidden', 'true');
    }
}

function setTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('adp-theme', theme);
    var isDark = theme === 'dark';
    document.querySelector('meta[name=theme-color]').content = isDark ? '#0b1220' : '#0f172a';
    var icon = isDark
        ? '<svg class="btn-icon" viewBox="0 0 24 24" width="16" height="16" aria-hidden="true"><circle cx="12" cy="12" r="5" fill="currentColor"/><g stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="12" y1="1" x2="12" y2="3"/><line x1="12" y1="21" x2="12" y2="23"/><line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/><line x1="1" y1="12" x2="3" y2="12"/><line x1="21" y1="12" x2="23" y2="12"/><line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/></g></svg>'
        : '<svg class="btn-icon" viewBox="0 0 24 24" width="16" height="16" aria-hidden="true"><path fill="currentColor" d="M12 3a9 9 0 1 0 9 9c0-.46-.04-.92-.1-1.36a5.39 5.39 0 0 1-4.4 2.26 5.38 5.38 0 0 1-5.38-5.38 5.39 5.39 0 0 1 2.26-4.4A9.05 9.05 0 0 0 12 3Z"/></svg>';
    $('#themeToggle').attr('aria-pressed', isDark ? 'true' : 'false').attr('title', 'Alternar entre modo oscuro y claro').html(icon + '<span>' + (isDark ? 'Modo claro' : 'Modo oscuro') + '</span>');
}

function initTheme() {
    var theme = localStorage.getItem('adp-theme');
    if (theme !== 'dark' && theme !== 'light') theme = 'dark';
    setTheme(theme);
}

var ESCAPE_MAP = { '&': '&amp;', '"': '&quot;', "'": '&#39;', '<': '&lt;', '>': '&gt;' };
function escapeHtmlAttr(s) {
    return (s || '').replace(/[&"'<>]/g, function (c) { return ESCAPE_MAP[c]; });
}

var REF_MAP = ['-', 'Yrigoyen', 'Nico', 'Norma', 'Tia Raquel', 'Tia Renee', 'Velez Sarsfield', 'Villa Gesell'];
function getStringReference(nroRerence) {
    var nro = typeof nroRerence === 'string' ? parseInt(nroRerence, 10) : nroRerence;
    return (nro >= 0 && nro < REF_MAP.length) ? REF_MAP[nro] : '-';
}

var REFERENCIAS = [
    { key: 'Yrigoyen',       icon: '\uD83C\uDFE0' },
    { key: 'Norma',          icon: '\uD83D\uDC64' },
    { key: 'Nico',           icon: '\uD83D\uDC64' },
    { key: 'Tia Raquel',     icon: '\uD83D\uDC64' },
    { key: 'Tia Renee',      icon: '\uD83D\uDC64' },
    { key: 'Velez Sarsfield',icon: '\uD83C\uDFE0' },
    { key: 'Villa Gesell',   icon: '\uD83C\uDFD6\uFE0F' },
];

function renderRefCards(totals) {
    var html = '';
    REFERENCIAS.forEach(function (r) {
        var t = totals[r.key];
        if (!t.ars && !t.usd) return;
        html += '<article class="stat-card stat-card-ref">';
        html += '<p class="stat-label">' + r.icon + ' ' + r.key + '</p>';
        html += '<p class="stat-value-ref-ars">' + formatArs(t.ars) + '</p>';
        html += '<p class="stat-value-ref-usd">' + formatUsd(t.usd) + '</p>';
        html += '</article>';
    });
    $('.stats-grid-ref').html(html || '<p class="stats-ref-empty">Sin datos</p>');
}

function totalizarPagos() {
    var dt = $('#dataTable').dataTable().api();
    var rows = dt.rows({ search: 'applied' }).data();
    var totalPesos = 0;
    var totalDolares = 0;
    var refTotals = {};

    REFERENCIAS.forEach(function (r) { refTotals[r.key] = { ars: 0, usd: 0 }; });

    rows.each(function (item) {
        var importe = Number(item[9]) || 0;
        var importeUsd = Number(item[10]) || 0;
        totalPesos += importe;
        totalDolares += importeUsd;

        var ref = (item[11] || '').toString();
        if (refTotals[ref]) {
            refTotals[ref].ars += importe;
            refTotals[ref].usd += importeUsd;
        }
    });

    $('#pesosCalculados').text(formatArs(totalPesos));
    $('#dolaresCalculados').text(formatUsd(totalDolares));

    renderRefCards(refTotals);
}

function leerPdf(action) {
    var endpoint = action || 'LeerPDF';
    setLoading(true);
    limpiarMensajes();

    table.clear().draw(false);

    $.ajax({
        url: '/Home/' + endpoint + '/',
        success: function (respuesta) {
            var lstMsj = respuesta.Mensajes.Lista;
            if (lstMsj.length > 0) {
                procesarMensajes(lstMsj);
                return;
            }

            var data = respuesta.pagos.map(function (pago) {
                var safePath = escapeHtmlAttr(pago.Path);
                return [
                    pago.NroTransaccion,
                    pago.Ente,
                    pago.NroCliente,
                    pago.NroCtaDebito,
                    parseMvcDate(pago.FechaVencimiento),
                    pago.Cuota,
                    '<button type="button" class="pdf-link" data-path="' + safePath + '" title="Abrir PDF en nueva pesta\u00f1a">Abrir</button>',
                    pago.TipoComprobante,
                    parseMvcDate(pago.FechaPago),
                    Number(pago.Importe) || 0,
                    Number(pago.ImporteDolar) || 0,
                    getStringReference(pago.Referencia)
                ];
            });

            table.rows.add(data).draw();

            callBackLeerPdf();

            if (typeof respuesta.cantNoAbiertos !== 'undefined') {
                var total = (respuesta.cantNoAbiertos || 0) + (respuesta.cantNoIdentificados || 0) + (respuesta.cantNoValidos || 0);
                var el = $('#noProcesados');
                el.text(total);
                el.attr('title', 'No se pudieron abrir: ' + (respuesta.cantNoAbiertos || 0)
                    + ' | No se pudieron identificar: ' + (respuesta.cantNoIdentificados || 0)
                    + ' | Datos incompletos: ' + (respuesta.cantNoValidos || 0));
                window._fallosData = {
                    noAbiertosPaths: respuesta.noAbiertosPaths || [],
                    noIdentificadosPaths: respuesta.noIdentificadosPaths || [],
                    noValPaths: respuesta.noValPaths || []
                };
            }
        },
        error: function (err) {
            procesarMensajes([{ Tipo: 0, Texto: 'No se pudo actualizar la informacion. Estado: ' + err.status }]);
            if (err.responseText) {
                $('body').html(err.responseText);
            }


        },
        complete: function () {
            setLoading(false);
        }
    });
}

function callBackLeerPdf() {
    table.columns.adjust().draw(false);
    totalizarPagos();
}

function applyAdvancedFilters() {
    table.draw();
    totalizarPagos();
}

function fileNameFromPath(path) {
    return path.split('\\').pop().split('/').pop();
}

function dirFromPath(path) {
    var idx = Math.max(path.lastIndexOf('\\'), path.lastIndexOf('/'));
    return idx > 0 ? path.substring(0, idx) : path;
}

function openFileFolder(path) {
    $.ajax({
        url: '/Home/OpenFolder',
        data: { path: path },
        error: function () {
            procesarMensajes([{ Tipo: 0, Texto: 'No se pudo abrir la carpeta. Verific\u00e1 que la ruta exista.' }]);
        }
    });
}

function renderFallosModal(data) {
    var sections = [
        { key: 'noAbiertosPaths', label: 'No se pudieron abrir', icon: '\uD83D\uDD12', count: data.noAbiertosPaths.length },
        { key: 'noIdentificadosPaths', label: 'No se pudieron identificar', icon: '\u2753', count: data.noIdentificadosPaths.length },
        { key: 'noValPaths', label: 'Datos incompletos', icon: '\u26A0\uFE0F', count: data.noValPaths.length }
    ];
    var html = '';
    html += '<div class="fallos-filtro-wrap">';
    html += '<input type="text" id="fallosFiltro" class="fallos-filtro-input" placeholder="Filtrar archivos..." title="Filtrar archivos por nombre">';
    html += '</div>';
    sections.forEach(function (sec) {
        var paths = data[sec.key] || [];
        var isEmpty = paths.length === 0;
        var collapsed = sec.count > 10 ? ' is-collapsed' : '';
        html += '<div class="fallos-section' + collapsed + '">';
        html += '<h3 class="fallos-section-title" tabindex="0" role="button" aria-expanded="' + (collapsed ? 'false' : 'true') + '">';
        html += '<span class="collapse-arrow">&#x25B6;</span>';
        html += sec.icon + ' ' + sec.label;
        html += ' <span class="fallos-count">' + sec.count + '</span>';
        html += '</h3>';
        html += '<div class="fallos-section-body">';
        if (isEmpty) {
            html += '<p class="fallos-empty">Ninguno</p>';
        } else {
            html += '<ul class="fallos-list">';
            paths.forEach(function (p) {
                var name = fileNameFromPath(p);
                var safePath = p.replace(/"/g, '&quot;');
                html += '<li class="fallos-item" data-path="' + safePath + '" title="' + safePath + '">';
                html += '<button type="button" class="fallos-btn fallos-btn-dir" data-path="' + safePath + '" title="Abrir carpeta contenedora">';
                html += '<svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true"><path fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2v11z"/></svg>';
                html += '</button>';
                html += '<button type="button" class="fallos-btn fallos-btn-pdf" data-path="' + safePath + '" title="Abrir PDF en ventana nueva">';
                html += '<svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true"><path fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6M15 3h6v6M10 14 21 3"/></svg>';
                html += '</button>';
                html += '<span class="fallos-filename">' + name + '</span>';
                html += '</li>';
            });
            html += '</ul>';
        }
        html += '</div></div>';
    });
    $('#modalFallosBody').html(html);

    var debounceFilterTimer;
    $('#fallosFiltro').on('input', function () {
        clearTimeout(debounceFilterTimer);
        debounceFilterTimer = setTimeout(function () {
            var q = this.value.toLowerCase();
            $('#modalFallosBody .fallos-item').each(function () {
                var name = $(this).find('.fallos-filename').text().toLowerCase();
                $(this).toggle(name.indexOf(q) !== -1);
            });
            $('#modalFallosBody .fallos-section').each(function () {
                var visibleItems = $(this).find('.fallos-item:visible').length;
                var totalItems = $(this).find('.fallos-item').length;
                if (totalItems === 0) return;
                $(this).toggle(visibleItems > 0);
            });
        }.bind(this), 150);
    });
}

function abrirModalFallos() {
    if (window._fallosData) {
        renderFallosModal(window._fallosData);
    }
    $('#modalFallos').removeAttr('aria-hidden');
    $('body').addClass('no-scroll');
}

function cerrarModalFallos() {
    $('#modalFallos').attr('aria-hidden', 'true');
    $('body').removeClass('no-scroll');
}

$(document).ready(function () {
    initTheme();

    $('#themeToggle').on('click', function () {
        var current = document.documentElement.getAttribute('data-theme');
        setTheme(current === 'dark' ? 'light' : 'dark');
    });

    $('#btnToggleFiltros').on('click', function () {
        var panel = $('#filtersPanel');
        var isCollapsed = panel.hasClass('is-collapsed');
        panel.toggleClass('is-collapsed', !isCollapsed);
        $(this).attr('aria-expanded', isCollapsed ? 'true' : 'false').attr('title', 'Mostrar u ocultar el panel de filtros').text(isCollapsed ? 'Ocultar filtros' : 'Mostrar filtros');
    });

    $(document).on('click', function (e) {
        if (window.innerWidth > 820) return;
        var panel = $('#filtersPanel');
        var toggle = $('#btnToggleFiltros');
        if (!panel.hasClass('is-collapsed') && !$(e.target).closest('#filtersPanel, #btnToggleFiltros').length) {
            panel.addClass('is-collapsed');
            toggle.attr('aria-expanded', 'false').attr('title', 'Mostrar u ocultar el panel de filtros').text('Mostrar filtros');
        }
    });

    $('#btnAplicarFiltros').on('click', applyAdvancedFilters);
    $('#btnLimpiarFiltros').on('click', function () {
        $('#ImporteDesde').val('');
        $('#ImporteHasta').val('');
        $('#selectReference').val('');
        $('#selectEnte').val('');
        applyAdvancedFilters();
    });

    $('#btnActualizarPdf').on('click', function () {
        leerPdf('ActualizarPDF');
    });

    $('#btnDetalleFallos').on('click', abrirModalFallos);
    $('#modalFallosCerrar, #modalFallosCerrarBtn').on('click', cerrarModalFallos);
    $('#modalFallos').on('click', function (e) {
        if (e.target === this) cerrarModalFallos();
    });
    $(document).on('keydown', function (e) {
        if (e.key === 'Escape') {
            cerrarModalFallos();
            var panel = $('#filtersPanel');
            if (!panel.hasClass('is-collapsed')) {
                panel.addClass('is-collapsed');
                $('#btnToggleFiltros').attr('aria-expanded', 'false').attr('title', 'Mostrar u ocultar el panel de filtros').text('Mostrar filtros');
            }
        }
    });
    $('#modalFallosBody').on('click', '.fallos-btn-dir', function (e) {
        e.stopPropagation();
        openFileFolder($(this).attr('data-path'));
    });
    $('#modalFallosBody').on('click', '.fallos-btn-pdf', function (e) {
        e.stopPropagation();
        var ruta = $(this).attr('data-path');
        if (ruta) {
            window.open('/Home/GetReport?path=' + encodeURIComponent(ruta), '_blank');
        }
    });
    $('#modalFallosBody').on('click', '.fallos-section-title', function () {
        var section = $(this).closest('.fallos-section');
        var isCollapsed = section.hasClass('is-collapsed');
        section.toggleClass('is-collapsed', !isCollapsed);
        $(this).attr('aria-expanded', isCollapsed ? 'true' : 'false');
    });

    $('#dataTable tbody').on('click', '.pdf-link', function (e) {
        e.stopPropagation();
        var ruta = $(this).data('path');
        if (ruta) {
            window.open('/Home/GetReport?path=' + encodeURIComponent(ruta), '_blank');
        }
    });

    $('#dataTable tfoot th').each(function () {
        var title = $(this).text();
        $(this).html('<input type="text" placeholder="Buscar ' + title + '" />');
    });

    var _savedStateLoaded = false;

    table = $('#dataTable').DataTable({
        stateSave: true,
        stateLoadParams: function (settings, data) {
            if (data) {
                _savedStateLoaded = true;
                if (data.advancedFilters) {
                    $('#ImporteDesde').val(data.advancedFilters.importeDesde || '');
                    $('#ImporteHasta').val(data.advancedFilters.importeHasta || '');
                    $('#selectReference').val(data.advancedFilters.selectReference || '');
                    $('#selectEnte').val(data.advancedFilters.selectEnte || '');
                }
                if (data.filtersCollapsed) {
                    $('#filtersPanel').addClass('is-collapsed');
                    $('#btnToggleFiltros').attr('aria-expanded', 'false').text('Mostrar filtros');
                }
            }
        },
        stateSaveParams: function (settings, data) {
            data.advancedFilters = {
                importeDesde: $('#ImporteDesde').val(),
                importeHasta: $('#ImporteHasta').val(),
                selectReference: $('#selectReference').val(),
                selectEnte: $('#selectEnte').val()
            };
            data.filtersCollapsed = $('#filtersPanel').hasClass('is-collapsed');
        },
        aoColumnDefs: [
            { bSortable: false, aTargets: [6] },
            { sWidth: '120px', aTargets: [4, 8, 9, 10] },
            { targets: [9, 10], className: 'dt-right', render: function (data, type) {
                if (type === 'display') {
                    return new Intl.NumberFormat('es-AR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(data || 0);
                }
                return data;
            }}
        ],
        bAutoWidth: false,
        orderCellsTop: true,
        language: {
            search: 'Buscar:',
            info: 'Mostrando _START_ a _END_ de _TOTAL_ pagos',
            infoEmpty: 'Sin pagos disponibles',
            infoFiltered: ' (filtrados de _MAX_ pagos totales)',
            lengthMenu: 'Mostrar _MENU_ pagos',
            paginate: {
                previous: 'Anterior',
                next: 'Siguiente'
            },
            thousands: '.'
        },
        initComplete: function () {
            var api = this.api();
            if (!_savedStateLoaded) {
                api.columns([0, 2, 3, 5, 7]).visible(false, false);
                api.columns.adjust().draw(false);
            }
            api.columns().every(function () {
                var that = this;
                var debounceTimer;
                $('input', this.footer()).on('keyup change clear', function () {
                    clearTimeout(debounceTimer);
                    debounceTimer = setTimeout(function () {
                        if (that.search() !== this.value) {
                            that.search(this.value).draw();
                            totalizarPagos();
                        }
                    }.bind(this), 300);
                });
                // Sincronizar el input con la búsqueda restaurada por stateSave
                $('input', this.footer()).val(that.search());
            });
        }
    });

    $('button.toggle-vis').each(function () {
        var col = table.column($(this).attr('data-column'));
        $(this).attr('aria-pressed', col.visible() ? 'true' : 'false');
    });

    $('button.toggle-vis').on('click', function () {
        var col = table.column($(this).attr('data-column'));
        var next = !col.visible();
        col.visible(next);
        $(this).attr('aria-pressed', next ? 'true' : 'false');
    });

    $.fn.dataTable.ext.search.push(function (settings, data) {
        if (settings.nTable.id !== 'dataTable') {
            return true;
        }

        var min = parseFloat($('#ImporteDesde').val());
        var max = parseFloat($('#ImporteHasta').val());
        var importe = parseFloat(data[9]) || 0;

        if (!isNaN(min) && importe < min) {
            return false;
        }
        if (!isNaN(max) && importe > max) {
            return false;
        }

        var selectReference = $('#selectReference').val();
        if (selectReference !== '') {
            var txtReference = getStringReference(selectReference);
            if ((data[11] || '').toString() !== txtReference) {
                return false;
            }
        }

        var selectEnte = ($('#selectEnte').val() || '').toLowerCase();
        if (selectEnte !== '') {
            var enteDT = (data[1] || '').toLowerCase();
            if (enteDT.indexOf(selectEnte) === -1) {
                return false;
            }
        }

        return true;
    });

    leerPdf();
});
