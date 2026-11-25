using SolarOfThings.Core.SolarOfThings;
namespace SolarOfThings.Core.Utility;

public static class UtilityBillAuditReadings
{
    public static (UtilityMeterReading From, UtilityMeterReading To, bool Printed)
        Resolve(UtilityBillRecord bill, UtilityMeterReading? linkedFrom, UtilityMeterReading? linkedTo, bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;
        if (bill.ReviewState == UtilityBillReviewState.Reviewed &&
            bill.MeterStartKwh is double a && bill.MeterEndKwh is double b)
        {
            if (!double.IsFinite(a) || !double.IsFinite(b) || a < 0 || b < a ||
                (bill.BilledConsumptionKwh is double k && Math.Abs(b-a-k)>0.01))
                throw new InvalidOperationException(L("Las lecturas impresas no concilian con el consumo. Revisa la boleta.",
                    "Printed readings do not reconcile with consumption. Review the bill."));
            UtilityMeterReading Printed(DateTimeOffset at, double value) =>
                new(0,at,value,UtilityReadingSourceKind.UtilityOfficial,bill.PeriodPrecision,
                    bill.PeriodPrecision==UtilityTimePrecision.DateOnly ? UtilityTimeAssumption.StartOfDayAssumed : UtilityTimeAssumption.Exact,
                    bill.InvoiceReference,null,bill.CreatedUtc,bill.UpdatedUtc);
            return (Printed(bill.PeriodStartUtc,a),Printed(bill.PeriodEndUtc,b),true);
        }
        if (linkedFrom is not null && linkedTo is not null &&
            linkedFrom.SourceKind == UtilityReadingSourceKind.UtilityOfficial &&
            linkedTo.SourceKind == UtilityReadingSourceKind.UtilityOfficial &&
            linkedTo.ReadingAtUtc > linkedFrom.ReadingAtUtc && linkedTo.ReadingKwh >= linkedFrom.ReadingKwh)
            return (linkedFrom,linkedTo,false);
        throw new InvalidOperationException(L("La boleta no tiene ambas lecturas impresas revisadas ni lecturas oficiales vinculadas. Revisa las lecturas inicial y final.",
            "The bill has neither both reviewed printed readings nor linked official readings. Review its start and end readings."));
    }
}
