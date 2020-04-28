
var convertContenedor = function (contenedor) {
    var objContenedor = null;

    if (contenedor == undefined || contenedor == '') {
        objContenedor = $('#contentMensajes');
    } else {

        if (contenedor == '') {
            objContenedor = $(contenedor);
        }
    }

    return objContenedor;
};

var procesarMensajes = function (lstMsj, contenedor) {

    var objContenedor = convertContenedor(contenedor);

    lstMsj.forEach(function (msj) {
        
        let classColor = '';
        switch (msj.tipo) {
            case 0: //Error
                classColor = 'danger';
                break;
            case 1: //Informacion
                classColor = 'success';
                break;
            case 2: //Advertencia
                classColor = 'success';
                break;
            case 3: //Exito
                classColor = 'success';                
                break;
            case 4: //Advertencia
                classColor = 'primary';
                break;
            default:
                break;
        }

        let panelMsj = 
            `<div class="col-lg-12 card-msj">
                <!-- Basic Card Example -->
                <div class="card shadow mb-4">
                    <div class="card-header py-3">
                        <h6 class="m-0 font-weight-bold text-` + classColor + `">Basic Card Example</h6>
                    </div>
                    <div class="card-body">
                        ` + msj.texto + `
                    </div>
                </div>
            </div>`

        objContenedor.append(panelMsj);

        //let d = document.createElement('div');
        //$(d).addClass(classColor)
        //    .html(msj.texto)
        //    .appendTo(objContenedor)
    });
};

var limpiarMensajes = function (contenedor) {
    var objContenedor = convertContenedor(contenedor);
    objContenedor.children('.card-msj').remove()
};
