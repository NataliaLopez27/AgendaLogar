using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Seguridad;
using AgendaLogar.Core.Servicios;
using static AgendaLogar.Tests.EscenarioClientes;

namespace AgendaLogar.Tests;

/// <summary>Escenario del Hito 3: administrador, dos empleados y algunos clientes.</summary>
public sealed class EscenarioPeriodos : IDisposable
{
    public EscenarioClientes Clientes { get; } = new();
    public ServicioPeriodos Servicio { get; }
    public Usuario Nuria { get; }
    public Usuario Javi { get; }

    public EscenarioPeriodos()
    {
        Servicio = new ServicioPeriodos(Clientes.Bd, new ControlAcceso(Datos.SesionDe(Clientes.Admin)));
        Nuria = Datos.CrearUsuario(Clientes.Bd, "nuria", "secreta123", nombre: "Nuria");
        Javi = Datos.CrearUsuario(Clientes.Bd, "javi", "secreta123", nombre: "Javi");
    }

    public ServicioClientes ServicioClientes => Clientes.Servicio;

    public async Task<IReadOnlyList<AsignacionFila>> Filas(int periodoId, FiltroAsignaciones? f = null) =>
        await Servicio.AsignacionesAsync(periodoId, f);

    public void Dispose() => Clientes.Dispose();
}

public class CU15_CrearPeriodoTests
{
    [Fact]
    public async Task Crea_una_asignacion_pendiente_por_cada_cliente_vigente()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303", "130");
        var baja = await e.Clientes.CrearFisica("Berta", "X1234567L", "2020-01-01", "303");
        await e.ServicioClientes.DarDeBajaAsync(baja.Id, F("2026-06-30"));   // ya no entra en el 4T
        await e.Clientes.CrearFisica("Carla", "Y7654321G", "2027-01-01", "303"); // aún no ha empezado

        var r = await e.Servicio.CrearAsync(2026, 4, copiarAnterior: true);

        Assert.Equal("4T 2026", r.Nombre);
        Assert.Equal(1, r.Total);
        Assert.Equal(1, r.SinAsignar);
        var fila = Assert.Single(await e.Filas(r.PeriodoId));
        Assert.Equal("Ana", fila.Cliente);
        Assert.Equal("130, 303", fila.Modelos);
        Assert.Equal(EstadoAsignacion.Pendiente, fila.Estado);
        Assert.True(fila.SinAsignar);
    }

    [Fact]
    public async Task No_se_puede_crear_dos_veces_el_mismo_periodo()
    {
        using var e = new EscenarioPeriodos();
        await e.Servicio.CrearAsync(2026, 4, true);
        await e.Servicio.CrearAsync(2026, null, true);

        var t = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.CrearAsync(2026, 4, true));
        var a = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.CrearAsync(2026, null, true));

        Assert.Equal("El periodo 4T 2026 ya existe.", t.Message);
        Assert.Equal("El periodo Anual 2026 ya existe.", a.Message);
    }

    [Fact]
    public async Task El_anual_solo_incluye_clientes_con_modelos_anuales()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Solo trimestrales", "12345678Z", "2020-01-01", "303");
        await e.Clientes.CrearFisica("Con renta", "X1234567L", "2020-01-01", "303", "100");

        var r = await e.Servicio.CrearAsync(2026, null, true);

        var fila = Assert.Single(await e.Filas(r.PeriodoId));
        Assert.Equal("Con renta", fila.Cliente);
        Assert.Equal("100", fila.Modelos);
    }

    [Fact]
    public async Task Valida_el_trimestre_y_el_ano()
    {
        using var e = new EscenarioPeriodos();
        await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.CrearAsync(2026, 5, true));
        await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.CrearAsync(1990, 1, true));
    }

    [Fact]
    public async Task La_lista_va_del_mas_reciente_al_mas_antiguo_con_su_progreso()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303", "100");
        await e.Servicio.CrearAsync(2026, 3, true);
        await e.Servicio.CrearAsync(2026, null, true);
        await e.Servicio.CrearAsync(2026, 4, true);

        var lista = await e.Servicio.ListarAsync();

        Assert.Equal(new[] { "Anual 2026", "4T 2026", "3T 2026" }, lista.Select(p => p.Nombre));
        Assert.All(lista, p => Assert.Equal("0 de 1 listos para mandar", p.Progreso));
        Assert.All(lista, p => Assert.Equal("1 sin asignar", p.SinAsignarTexto));
    }

    [Fact]
    public async Task Un_empleado_no_puede_gestionar_periodos()
    {
        using var e = new EscenarioPeriodos();
        var servicio = new ServicioPeriodos(e.Clientes.Bd, new ControlAcceso(Datos.SesionDe(e.Nuria)));

        await Assert.ThrowsAsync<AccesoDenegadoException>(() => servicio.ListarAsync());
        await Assert.ThrowsAsync<AccesoDenegadoException>(() => servicio.CrearAsync(2026, 4, true));
    }
}

public class RF41_CopiarAsignacionesTests
{
    [Fact]
    public async Task Copia_el_empleado_del_trimestre_anterior_y_los_nuevos_quedan_sin_asignar()
    {
        using var e = new EscenarioPeriodos();
        var ana = await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303");
        var t3 = await e.Servicio.CrearAsync(2026, 3, true);
        await e.Servicio.AsignarAsync(t3.PeriodoId, (await e.Filas(t3.PeriodoId)).Select(f => f.Id).ToList(), e.Nuria.Id);
        await e.Clientes.CrearFisica("Nuevo", "X1234567L", "2026-10-05", "303");

        var t4 = await e.Servicio.CrearAsync(2026, 4, copiarAnterior: true);

        Assert.Equal(1, t4.Asignados);
        Assert.Equal(1, t4.SinAsignar);
        var filas = await e.Filas(t4.PeriodoId);
        Assert.Equal("Nuria", filas.Single(f => f.ClienteId == ana.Id).Empleado);
        Assert.True(filas.Single(f => f.Cliente == "Nuevo").SinAsignar);
    }

    [Fact]
    public async Task Sin_copiar_todos_quedan_sin_asignar()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303");
        var t3 = await e.Servicio.CrearAsync(2026, 3, true);
        await e.Servicio.AsignarAsync(t3.PeriodoId, (await e.Filas(t3.PeriodoId)).Select(f => f.Id).ToList(), e.Nuria.Id);

        var t4 = await e.Servicio.CrearAsync(2026, 4, copiarAnterior: false);

        Assert.Equal(0, t4.Asignados);
    }

    [Fact]
    public async Task El_1T_copia_del_4T_del_ano_anterior_y_excluye_las_bajas()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303");
        var berta = await e.Clientes.CrearFisica("Berta", "X1234567L", "2020-01-01", "303");
        var t4 = await e.Servicio.CrearAsync(2026, 4, true);
        await e.Servicio.AsignarAsync(t4.PeriodoId, (await e.Filas(t4.PeriodoId)).Select(f => f.Id).ToList(), e.Javi.Id);
        await e.ServicioClientes.DarDeBajaAsync(berta.Id, F("2026-11-24"));

        var t1 = await e.Servicio.CrearAsync(2027, 1, true);

        var fila = Assert.Single(await e.Filas(t1.PeriodoId));
        Assert.Equal("Ana", fila.Cliente);
        Assert.Equal("Javi", fila.Empleado);
        Assert.Equal(new[] { "Berta" }, t1.BajasDesdeAnterior);
        Assert.Equal("4T 2026", t1.PeriodoAnterior);
        Assert.Contains("1 cliente(s) del 4T 2026 ya no están vigentes", t1.Resumen);
    }

    [Fact]
    public async Task Si_el_empleado_anterior_esta_desactivado_queda_sin_asignar()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303");
        var t3 = await e.Servicio.CrearAsync(2026, 3, true);
        await e.Servicio.AsignarAsync(t3.PeriodoId, (await e.Filas(t3.PeriodoId)).Select(f => f.Id).ToList(), e.Nuria.Id);
        await e.Servicio.CerrarAsync(t3.PeriodoId);
        using (var db = e.Clientes.Bd.CreateDbContext())
        {
            db.Usuarios.Single(u => u.Id == e.Nuria.Id).Activo = false;
            db.SaveChanges();
        }

        var t4 = await e.Servicio.CrearAsync(2026, 4, true);

        Assert.Equal(0, t4.Asignados);
    }
}

public class CU16_AsignarTests
{
    [Fact]
    public async Task Asigna_uno_o_varios_reasigna_y_quita()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303");
        await e.Clientes.CrearFisica("Berta", "X1234567L", "2020-01-01", "303");
        var p = await e.Servicio.CrearAsync(2026, 4, true);
        var filas = await e.Filas(p.PeriodoId);

        Assert.Equal(2, await e.Servicio.AsignarAsync(p.PeriodoId, filas.Select(f => f.Id).ToList(), e.Nuria.Id));
        await e.Servicio.AsignarAsync(p.PeriodoId, new[] { filas[0].Id }, e.Javi.Id);
        await e.Servicio.AsignarAsync(p.PeriodoId, new[] { filas[1].Id }, null);

        var despues = await e.Filas(p.PeriodoId);
        Assert.Equal("Javi", despues.Single(f => f.Id == filas[0].Id).Empleado);
        Assert.True(despues.Single(f => f.Id == filas[1].Id).SinAsignar);
    }

    [Fact]
    public async Task Queda_registro_en_el_historial()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303");
        var p = await e.Servicio.CrearAsync(2026, 4, true);
        var id = (await e.Filas(p.PeriodoId)).Single().Id;

        await e.Servicio.AsignarAsync(p.PeriodoId, new[] { id }, e.Nuria.Id);

        using var db = e.Clientes.Bd.CreateDbContext();
        var h = db.HistorialAsignaciones.Single();
        Assert.Equal("Empleado", h.Campo);
        Assert.Equal(e.Nuria.Id.ToString(), h.ValorNuevo);
        Assert.Equal(e.Clientes.Admin.Id, h.UsuarioId);
    }

    [Fact]
    public async Task El_administrador_puede_asignarse_clientes()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303");
        var p = await e.Servicio.CrearAsync(2026, 4, true);

        await e.Servicio.AsignarAsync(p.PeriodoId, new[] { (await e.Filas(p.PeriodoId)).Single().Id }, e.Clientes.Admin.Id);

        Assert.Equal("Jefe", (await e.Filas(p.PeriodoId)).Single().Empleado);
        Assert.Contains(await e.Servicio.EmpleadosAsignablesAsync(), u => u.Id == e.Clientes.Admin.Id);
    }

    [Fact]
    public async Task No_se_asigna_a_un_usuario_desactivado_ni_en_un_periodo_cerrado()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303");
        var p = await e.Servicio.CrearAsync(2026, 4, true);
        var id = (await e.Filas(p.PeriodoId)).Single().Id;
        using (var db = e.Clientes.Bd.CreateDbContext())
        {
            db.Usuarios.Single(u => u.Id == e.Javi.Id).Activo = false;
            db.SaveChanges();
        }

        var desactivado = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.AsignarAsync(p.PeriodoId, new[] { id }, e.Javi.Id));
        await e.Servicio.CerrarAsync(p.PeriodoId);
        var cerrado = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.AsignarAsync(p.PeriodoId, new[] { id }, e.Nuria.Id));

        Assert.Equal("El empleado no existe o está desactivado.", desactivado.Message);
        Assert.Contains("está cerrado", cerrado.Message);
    }

    [Fact]
    public async Task No_acepta_asignaciones_de_otro_periodo()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303");
        var t3 = await e.Servicio.CrearAsync(2026, 3, true);
        var t4 = await e.Servicio.CrearAsync(2026, 4, true);
        var deT3 = (await e.Filas(t3.PeriodoId)).Single().Id;

        await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.AsignarAsync(t4.PeriodoId, new[] { deT3 }, e.Nuria.Id));
    }

    [Fact]
    public async Task Filtra_sin_asignar_por_empleado_por_tipo_y_por_texto()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Ana López", "12345678Z", "2020-01-01", "303");
        await e.Clientes.CrearFisica("Berta", "X1234567L", "2020-01-01", "303");
        await e.ServicioClientes.CrearAsync(new DatosCliente("Construcciones SL", "B12345674", TipoCliente.Sociedad, null),
            F("2020-01-01"), new[] { new ModeloAlta("303", F("2020-01-01")) });
        var p = await e.Servicio.CrearAsync(2026, 4, true);
        var ana = (await e.Filas(p.PeriodoId)).Single(f => f.Cliente == "Ana López");
        await e.Servicio.AsignarAsync(p.PeriodoId, new[] { ana.Id }, e.Nuria.Id);

        Assert.Equal(2, (await e.Filas(p.PeriodoId, new FiltroAsignaciones(SoloSinAsignar: true))).Count);
        Assert.Equal("Ana López", (await e.Filas(p.PeriodoId, new FiltroAsignaciones(EmpleadoId: e.Nuria.Id))).Single().Cliente);
        Assert.Equal("Construcciones SL", (await e.Filas(p.PeriodoId, new FiltroAsignaciones(Tipo: TipoCliente.Sociedad))).Single().Cliente);
        Assert.Equal("Ana López", (await e.Filas(p.PeriodoId, new FiltroAsignaciones("lopez"))).Single().Cliente);
    }
}

public class CU17_CerrarReabrirTests
{
    [Fact]
    public async Task Cierra_y_reabre()
    {
        using var e = new EscenarioPeriodos();
        var p = await e.Servicio.CrearAsync(2026, 4, true);

        await e.Servicio.CerrarAsync(p.PeriodoId);
        Assert.Equal("Cerrado", (await e.Servicio.ListarAsync()).Single().EstadoTexto);
        var otraVez = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.CerrarAsync(p.PeriodoId));
        await e.Servicio.ReabrirAsync(p.PeriodoId);

        Assert.True((await e.Servicio.ListarAsync()).Single().Abierto);
        Assert.Equal("El 4T 2026 ya está cerrado.", otraVez.Message);
    }
}

public class RN03_RN04_RN05_PeriodosCreadosTests
{
    [Fact]
    public async Task RN03_la_baja_dentro_de_un_periodo_creado_no_lo_quita_y_RN05_muestra_la_etiqueta()
    {
        using var e = new EscenarioPeriodos();
        var ana = await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303");
        var p = await e.Servicio.CrearAsync(2026, 4, true);

        await e.ServicioClientes.DarDeBajaAsync(ana.Id, F("2026-11-24"));

        var fila = Assert.Single(await e.Filas(p.PeriodoId));
        Assert.Equal("Baja 24/11/2026", fila.EtiquetaBaja);
        Assert.Equal(EstadoAsignacion.Pendiente, fila.Estado);
    }

    [Fact]
    public async Task RN04_el_alta_dentro_de_un_periodo_abierto_se_puede_anadir()
    {
        using var e = new EscenarioPeriodos();
        var t4 = await e.Servicio.CrearAsync(2026, 4, true);
        var cerrado = await e.Servicio.CrearAsync(2026, 3, true);
        await e.Servicio.CerrarAsync(cerrado.PeriodoId);
        var nuevo = await e.Clientes.CrearFisica("Nuevo", "12345678Z", "2026-07-15", "303");

        var propuestos = await e.Servicio.PeriodosAbiertosSinClienteAsync(nuevo.Id);
        Assert.Equal(new[] { "4T 2026" }, propuestos.Select(p => p.Nombre));

        await e.Servicio.AnadirClienteAPeriodosAsync(nuevo.Id, propuestos.Select(p => p.Id).ToList());

        Assert.Single(await e.Filas(t4.PeriodoId));
        Assert.Empty(await e.Servicio.PeriodosAbiertosSinClienteAsync(nuevo.Id));
    }

    [Fact]
    public async Task RN04_no_propone_periodos_en_los_que_no_es_vigente()
    {
        using var e = new EscenarioPeriodos();
        await e.Servicio.CrearAsync(2026, 4, true);
        var futuro = await e.Clientes.CrearFisica("Futuro", "12345678Z", "2027-02-01", "303");

        Assert.Empty(await e.Servicio.PeriodosAbiertosSinClienteAsync(futuro.Id));
    }
}
