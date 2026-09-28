using System.Reflection;

namespace SolarOfThings.App;

internal static class ProductInfo
{
    public const string ProductVersion = "0.10.0";

    public static string InformationalVersion =>
        Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? ProductVersion + "+build.local.dev";

    public static string BuildNumber
    {
        get
        {
            var info = InformationalVersion;
            const string marker = "+build.";
            var start = info.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return "local";

            start += marker.Length;
            var end = info.IndexOf('.', start);
            return end > start ? info[start..end] : info[start..];
        }
    }

    public static string SourceRevision
    {
        get
        {
            var info = InformationalVersion;
            const string marker = "+build.";
            var start = info.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return "dev";

            start += marker.Length;
            var separator = info.IndexOf('.', start);
            if (separator < 0 || separator + 1 >= info.Length)
                return "dev";

            var revision = info[(separator + 1)..];
            return revision.Length > 8 ? revision[..8] : revision;
        }
    }

    public static string Display =>
        $"v{ProductVersion} · Build {BuildNumber} · {SourceRevision}";

    public static string ReleaseNotes(bool spanish)
    {
        if (spanish)
        {
            return $"""
                {Display}

                Novedades de esta build
                • La versión y el número de build quedan visibles permanentemente en la barra inferior y en Acerca de.
                • La ventana principal abre con mayor ancho para reducir recortes horizontales.
                • Las exportaciones muestran actividad visible mientras se están generando.
                • Nueva sección Acerca de con notas de versión.
                • Nueva sección Ayuda con manual integrado y exportación a PDF por sección o completa.
                • Nuevo indicador visual de apertura mientras se inicializa la aplicación.
                • Nueva importación asistida de datos desde otra instalación, con avance, respaldo automático y reinicio.
                • Si la instalación importada contiene una sesión recordada que Windows puede descifrar, se habilita la reconexión automática.

                QA pendiente registrado
                • La conciliación Enel debe separarse en análisis personal y auditoría/contraste de boleta.
                • La exportación de auditoría debe partir desde una boleta específica y sus lecturas oficiales vinculadas.
                • Tarifas oficiales: captura histórica, no limitada al año actual, normalización y aplicabilidad.
                • La diferencia de energía debe incluir incertidumbre, intervalo de confianza y contraste estadístico antes de calificarse como discrepancia material.
                """;
        }

        return $"""
            {Display}

            What's new in this build
            • Version and build are permanently visible in the footer and About page.
            • The main window opens wider by default to reduce horizontal clipping.
            • Exports show visible activity while files are being generated.
            • New About section with release notes.
            • New Help section with an integrated manual and PDF export by section or for the full manual.
            • New startup indicator while the application initializes.
            • New guided data import from another installation, with progress, automatic backup and restart.
            • If the imported installation contains a remembered session that Windows can decrypt, automatic reconnection is enabled.

            Recorded pending QA
            • Enel reconciliation must distinguish personal analysis from bill audit/utility dispute evidence.
            • Audit export must start from a specific bill and its linked official readings.
            • Official tariffs require historical capture, not just the current year, plus normalization and applicability.
            • Energy differences require uncertainty, confidence intervals and statistical comparison before being described as material discrepancies.
            """;
    }
}
