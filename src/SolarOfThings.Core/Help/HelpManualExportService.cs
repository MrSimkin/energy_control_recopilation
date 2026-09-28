using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace SolarOfThings.Core.Help;

public sealed record HelpManualSection(
    string Id,
    string TitleEs,
    string BodyEs,
    string TitleEn,
    string BodyEn)
{
    public string Title(bool spanish) => spanish ? TitleEs : TitleEn;
    public string Body(bool spanish) => spanish ? BodyEs : BodyEn;
}

public static class HelpManualCatalog
{
    public static IReadOnlyList<HelpManualSection> Sections { get; } =
    [
        new(
            "overview",
            "1. Qué hace Solar Energy Monitor",
            """
            Solar Energy Monitor guarda localmente la telemetría obtenida desde Solar of Things y permite revisar producción solar, consumo de la casa, uso de batería y energía importada desde la red. Los cálculos conservan los timestamps reales: los períodos sin datos no se inventan ni se rellenan como cero.

            La aplicación separa tres niveles: evidencia cruda descargada, métricas físicas normalizadas y análisis contextual del hogar. Esa separación permite revisar los resultados y evita convertir una interpretación familiar en una medición física.
            """,
            "1. What Solar Energy Monitor does",
            """
            Solar Energy Monitor stores Solar of Things telemetry locally and lets you review solar production, household consumption, battery use and energy imported from the grid. Calculations preserve real timestamps: missing periods are not invented or filled as zero.

            The application separates raw downloaded evidence, normalized physical metrics and household-context analysis. This separation keeps results auditable and prevents household interpretation from being presented as physical measurement.
            """),
        new(
            "dashboard",
            "2. Inicio",
            """
            Inicio muestra la información más reciente disponible para solar, casa, batería y red. Cuando existe una lectura actual suficientemente reciente, la aplicación la identifica como actual; de lo contrario conserva una presentación explícita de última información guardada.

            El resumen del último día guardado muestra energía y cobertura. Una cobertura baja significa que faltó parte del período y que el total no debe interpretarse como si existieran mediciones continuas.
            """,
            "2. Home",
            """
            Home shows the latest available solar, household, battery and grid information. When a sufficiently recent current reading exists it is identified as current; otherwise the app explicitly presents the latest saved information.

            The latest saved day summary includes energy and coverage. Low coverage means part of the interval is missing and totals must not be interpreted as continuous measurement.
            """),
        new(
            "history",
            "3. Historial y gráficos",
            """
            Esta sección permite elegir rangos y agregaciones por hora, día, semana, mes o año. Los gráficos y la tabla detallada usan las mismas filas de agregación.

            Los huecos sin mediciones permanecen discontinuos. La energía se integra usando los tiempos reales entre muestras y los huecos largos se excluyen. La cobertura debe revisarse antes de comparar períodos.
            """,
            "3. History and charts",
            """
            This section supports hour, day, week, month and year ranges and aggregations. Charts and the detailed table use the same aggregation rows.

            Missing intervals remain discontinuous. Energy is integrated using real sample times and long gaps are excluded. Coverage should be reviewed before comparing periods.
            """),
        new(
            "battery",
            "4. Batería",
            """
            Batería muestra SOC, energía almacenada estimada, energía disponible antes del cambio normal a red, reserva para cortes y mediciones técnicas disponibles.

            Las estimaciones de kWh dependen de la capacidad útil configurada. Los umbrales observados del inversor tienen prioridad cuando están disponibles; la política familiar funciona como contexto o respaldo y cualquier diferencia debe quedar visible.
            """,
            "4. Battery",
            """
            Battery shows SOC, estimated stored energy, energy available before normal grid transfer, outage reserve and available technical measurements.

            kWh estimates depend on configured usable capacity. Observed inverter thresholds take priority when available; household policy acts as context or fallback and any mismatch should remain visible.
            """),
        new(
            "grid",
            "5. Red eléctrica y compañía",
            """
            La sección Red eléctrica conserva lecturas acumuladas del medidor, boletas y líneas de cargo. La comparación física usa importación total desde la red de Solar of Things sobre el mismo intervalo; no usa la atribución analítica Enel → Casa.

            Hay dos usos distintos que no deben confundirse. El análisis personal permite comparar lecturas elegidas por el usuario. La auditoría de una boleta debe partir desde una boleta específica, sus lecturas oficiales vinculadas, su período y las tarifas aplicables.

            Una diferencia de kWh no demuestra por sí sola un error de facturación. La auditoría debe considerar cobertura, incertidumbre temporal, incertidumbre de medición, tarifas vigentes y la estructura completa de la boleta. La versión actual mantiene esta auditoría avanzada como trabajo en progreso.
            """,
            "5. Grid and utility",
            """
            The Grid section preserves cumulative meter readings, bills and charge lines. Physical comparison uses total Solar of Things grid import over the same interval; it does not use the analytical Utility → Home attribution.

            Two different uses must remain separate. Personal analysis lets the user compare selected readings. A bill audit must start from a specific bill, its linked official readings, its period and applicable tariffs.

            A kWh difference alone does not prove a billing error. Audit work must consider coverage, timing uncertainty, measurement uncertainty, effective tariffs and the complete bill structure. Advanced audit remains work in progress in the current version.
            """),
        new(
            "reports",
            "6. Informes",
            """
            Informes genera archivos Excel y PDF a partir de los mismos cálculos y reglas de cobertura usados por la aplicación. Durante una exportación se muestra actividad visible para evitar que una operación larga parezca un bloqueo.

            Revise siempre período, agregación y cobertura antes de utilizar un informe para decisiones o comparaciones externas.
            """,
            "6. Reports",
            """
            Reports generates Excel and PDF files from the same calculations and coverage rules used by the application. Visible activity is shown during export so a long operation does not look like a frozen application.

            Always review range, aggregation and coverage before using a report for decisions or external comparisons.
            """),
        new(
            "data",
            "7. Datos y actualizaciones",
            """
            Actualizar datos continúa el historial guardado y conserva lo ya descargado. La sección muestra fechas cubiertas, días revisados, lecturas crudas y normalizadas y señales de problemas.

            Importar datos de otra instalación reemplaza el conjunto Data de la instalación actual mediante un proceso asistido. Antes de aplicarlo se conserva un respaldo completo del Data anterior. La aplicación debe reiniciarse para realizar el intercambio antes de abrir SQLite.
            """,
            "7. Data and updates",
            """
            Update Data continues the saved history and preserves data already downloaded. The page shows covered dates, reviewed days, raw and normalized readings and issue indicators.

            Import data from another installation replaces the current installation Data set through a guided process. A full backup of the previous Data set is kept before applying the import. The application restarts so the swap occurs before SQLite is opened.
            """),
        new(
            "connection",
            "8. Cuenta y conexión",
            """
            La conexión a Solar of Things puede recordar sesión y credenciales localmente mediante DPAPI de Windows. Esos secretos quedan cifrados para el usuario de Windows que los guardó.

            Al importar otra instalación en el mismo usuario de Windows, la sesión recordada puede restaurarse y la aplicación intenta reconectar automáticamente. Si Windows no puede descifrar los secretos importados, será necesario iniciar sesión nuevamente.
            """,
            "8. Account and connection",
            """
            Solar of Things connection can remember session and credentials locally using Windows DPAPI. These secrets are encrypted for the Windows user that saved them.

            When another installation is imported under the same Windows user, the remembered session can be restored and the app attempts automatic reconnection. If Windows cannot decrypt imported secrets, you must sign in again.
            """),
        new(
            "diagnostics",
            "9. Ayuda técnica",
            """
            Ayuda técnica permite revisar el estado de conexión y generar paquetes de diagnóstico. Los paquetes se diseñan para capturar evidencia de API, normalización y configuración sin convertir valores no validados en hechos.

            Use esta sección cuando una pantalla muestre datos inesperados, una descarga falle o se necesite evidencia para desarrollar una corrección.
            """,
            "9. Technical help",
            """
            Technical Help lets you review connection status and generate diagnostic bundles. Bundles are designed to capture API, normalization and configuration evidence without promoting unvalidated values to facts.

            Use this section when a screen shows unexpected data, a download fails or evidence is needed to develop a correction.
            """),
        new(
            "settings",
            "10. Configuración",
            """
            Configuración controla la conexión recordada y la reconexión automática. Olvidar credenciales elimina la sesión local protegida; no modifica la cuenta remota.

            La interfaz usa español por defecto y permite cambiar a inglés. La preferencia queda guardada localmente.
            """,
            "10. Settings",
            """
            Settings controls remembered connection and automatic reconnection. Forget credentials removes the protected local session; it does not modify the remote account.

            The interface defaults to Spanish and can be switched to English. The preference is stored locally.
            """),
        new(
            "quality",
            "11. Cobertura, confianza y límites",
            """
            Cobertura indica cuánto del intervalo pudo calcularse con evidencia disponible; no equivale a precisión metrológica. Un valor de cobertura alto no elimina error de sensor, diferencias de reloj, redondeo, calibración ni diferencias entre el medidor certificado y la telemetría del inversor.

            Para una conciliación frente a la compañía eléctrica se requiere además cuantificar incertidumbre y aplicar un contraste estadístico apropiado. La aplicación no debe etiquetar una diferencia como error de Enel sólo porque dos totales sean distintos.
            """,
            "11. Coverage, confidence and limits",
            """
            Coverage indicates how much of an interval could be calculated from available evidence; it is not metrological accuracy. High coverage does not remove sensor error, clock differences, rounding, calibration or differences between the certified utility meter and inverter telemetry.

            A utility-facing reconciliation also requires quantified uncertainty and an appropriate statistical comparison. The application must not label a difference as a utility error merely because two totals differ.
            """)
    ];
}

public sealed class HelpManualExportService
{
    private static int _pdfFontsInitialized;

    public void ExportPdf(
        string path,
        string languageCode,
        string? sectionId = null,
        string? versionDisplay = null)
    {
        EnsurePdfFonts();

        var spanish = languageCode.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        IReadOnlyList<HelpManualSection> selected =
            string.IsNullOrWhiteSpace(sectionId)
                ? HelpManualCatalog.Sections
                : HelpManualCatalog.Sections
                    .Where(section => string.Equals(section.Id, sectionId, StringComparison.Ordinal))
                    .ToArray();

        if (selected.Count == 0)
            throw new InvalidOperationException("The requested help section does not exist.");

        var document = new Document();
        document.Info.Title = spanish
            ? "Manual de Solar Energy Monitor"
            : "Solar Energy Monitor Manual";

        var normal = document.Styles["Normal"];
        normal.Font.Name = "Arial";
        normal.Font.Size = 10;

        var section = document.AddSection();
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.6);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.6);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.8);

        var title = section.AddParagraph(document.Info.Title);
        title.Format.Font.Size = 22;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = Colors.DarkSlateGray;
        title.Format.SpaceAfter = Unit.FromPoint(4);

        var subtitle = section.AddParagraph(
            versionDisplay ?? (spanish ? "Guía integrada de uso" : "Integrated user guide"));
        subtitle.Format.Font.Size = 9;
        subtitle.Format.Font.Color = Colors.DimGray;
        subtitle.Format.SpaceAfter = Unit.FromPoint(14);

        foreach (var item in selected)
        {
            var heading = section.AddParagraph(item.Title(spanish));
            heading.Format.Font.Size = 15;
            heading.Format.Font.Bold = true;
            heading.Format.Font.Color = Colors.SteelBlue;
            heading.Format.SpaceBefore = Unit.FromPoint(10);
            heading.Format.SpaceAfter = Unit.FromPoint(5);

            foreach (var paragraphText in item.Body(spanish)
                         .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries))
            {
                var paragraph = section.AddParagraph(paragraphText.Trim());
                paragraph.Format.SpaceAfter = Unit.FromPoint(7);
            }
        }

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 8;
        footer.Format.Font.Color = Colors.Gray;
        footer.AddText("Solar Energy Monitor · ");
        footer.AddPageField();

        var renderer = new PdfDocumentRenderer
        {
            Document = document
        };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(path);
    }

    private static void EnsurePdfFonts()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("PDF export is supported by the Windows desktop application.");

        if (Interlocked.Exchange(ref _pdfFontsInitialized, 1) == 0)
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }
}
