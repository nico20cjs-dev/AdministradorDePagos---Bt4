
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
            case 0: //Error
                classColor = 'error';
                title = 'Error';
                break;
            case 1: //Informacion
                classColor = 'info';
                title = 'Informacion';
                break;
            case 2: //Advertencia
                classColor = 'warning';
                title = 'Advertencia';
                break;
            case 3: //Exito
                classColor = 'success';                
                title = 'Exito';
                break;
            case 4: //Advertencia
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

//Icono de reloj de arena - Le cambia el icono cada 1 seg
function hourglass() {
    var a;
    a = document.getElementById("div1");
    if (a != null) {
        a.innerHTML = "&#xf251;";
        setTimeout(function () {
            a.innerHTML = "&#xf252;";
        }, 1000);
        setTimeout(function () {
            a.innerHTML = "&#xf253;";
        }, 2000);
    }
}
hourglass();
setInterval(hourglass, 3000);