using System.Globalization;

namespace SolarOfThings.Core.Utility;

/// <summary>
/// Distinguishes original printed-line discrepancy from reconstructed
/// tariff-model residual; does not create any balancing adjustment.
/// </summary>
public static class BillAuditEvidencePresentation
{
    public static string DescribePrintedDetailResidual(
        double printedTotalDueClp, double printedLineTotalClp, bool spanish)
    {
        if (!double.IsFinite(printedTotalDueClp) ||
            !double.IsFinite(printedLineTotalClp))
            throw new ArgumentOutOfRangeException(nameof(printedLineTotalClp));

        var residual = printedTotalDueClp - printedLineTotalClp;
        var signedClp = residual.ToString("+0;-0;0", CultureInfo.InvariantCulture);
        if (Math.Abs(residual) <= 0.5)
            return spanish
                ? "Residual del detalle impreso (total a pagar − suma de líneas): 0 CLP. Las líneas cuadran dentro de la tolerancia de 0,5 CLP."
                : "Printed-detail residual (total due − sum of lines): 0 CLP. The lines reconcile within the 0.5 CLP tolerance.";

        return spanish
            ? $"Residual del detalle impreso (total a pagar − suma de líneas): {signedClp} CLP. Distinto del residual del subtotal tarifario modelado. No se inventa un ajuste para cuadrar el detalle."
            : $"Printed-detail residual (total due − sum of lines): {signedClp} CLP. Distinct from the modeled tariff-subtotal residual. No balancing adjustment is invented.";
    }
}
