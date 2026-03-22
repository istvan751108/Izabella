using Izabella.Models.DTOs;
namespace Izabella.Services
{
    public class ManureCalculationService
    {
        public LiquidResult CalculateLiquid(double total)
        {
            var cow = Math.Round(total * 0.61);
            var y69 = Math.Round(total * 0.12);
            var y912 = Math.Round(total * 0.07);
            var y12p = Math.Round(total * 0.08);
            var preg = Math.Round(total * 0.12);

            var sum = cow + y69 + y912 + y12p + preg;
            var diff = total - sum;

            if (Math.Abs(diff) <= 1)
            {
                y12p += diff;
            }
            else
            {
                throw new Exception("Túl nagy kerekítési eltérés!");
            }

            return new LiquidResult
            {
                Cow = cow,
                Young6_9 = y69,
                Young9_12 = y912,
                Young12Preg = y12p,
                PregnantHeifer = preg
            };
        }

        public double CalculateNet(double gross, double tare)
        {
            return gross - tare;
        }

        public SolidResult CalculateSolid(double total)
        {
            var cow = Math.Round(total * 0.50);
            var calfMilk = Math.Round(total * 0.06);
            var calf36 = Math.Round(total * 0.06);
            var y69 = Math.Round(total * 0.09);
            var y912 = Math.Round(total * 0.03);

            var sum = cow + calfMilk + calf36 + y69 + y912;
            var preg = total - sum;

            return new SolidResult
            {
                Cow = cow,
                CalfMilk = calfMilk,
                Calf3_6 = calf36,
                Young6_9 = y69,
                Young9_12 = y912,
                PregnantHeifer = preg
            };
        }
    }
}
