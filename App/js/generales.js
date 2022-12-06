
Date.prototype.yyyymmdd = function () {
    var mm = this.getMonth() + 1; // getMonth() is zero-based
    var dd = this.getDate();

    return [this.getFullYear() + '-', 
        (mm > 9 ? '' : '0') + mm + '-',
        (dd > 9 ? '' : '0') + dd
    ].join('');
};

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
        switch (msj.Tipo) {
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
                        ` + msj.Texto + `
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

function hourglass() {
    var a;
    a = document.getElementById("div1");
    a.innerHTML = "&#xf251;";
    setTimeout(function () {
        a.innerHTML = "&#xf252;";
    }, 1000);
    setTimeout(function () {
        a.innerHTML = "&#xf253;";
    }, 2000);
}
hourglass();
setInterval(hourglass, 3000);