namespace Izabella.Services
{
    using Izabella.Models;
    using Izabella.Models.DTOs;

    public class SolidManureService
    {
        private readonly IzabellaDbContext _context;
        private readonly ManureCalculationService _calc;
        private readonly VoucherService _voucher;

        public SolidManureService(
            IzabellaDbContext context,
            ManureCalculationService calc,
            VoucherService voucher)
        {
            _context = context;
            _calc = calc;
            _voucher = voucher;
        }

        public async Task<object> ProcessDailyAsync(DateTime date, List<SolidEntryDto> entries)
        {
            double totalNet = 0;

            var grouped = new Dictionary<string, double>();

            foreach (var e in entries)
            {
                var net = _calc.CalculateNet(e.Gross, e.Tare);
                totalNet += net;

                if (!grouped.ContainsKey(e.Destination))
                    grouped[e.Destination] = 0;

                grouped[e.Destination] += net;

                // egyedi beszállítás mentése
                _context.SolidManureLoads.Add(new SolidManureLoad
                {
                    Date = date,
                    LicensePlate = e.LicensePlate,
                    GrossWeight = e.Gross,
                    TareWeight = e.Tare,
                    NetWeight = net,
                    Destination = e.Destination
                });
            }

            // napi bontás
            var result = _calc.CalculateSolid(totalNet);

            var voucherIn = await _voucher.CreateVoucherAsync("+");

            var expenseMap = new Dictionary<string, string>();

            foreach (var dest in grouped.Keys)
            {
                var v = await _voucher.CreateVoucherAsync("-");
                expenseMap[dest] = _voucher.FormatVoucher(v);
            }
            // napi összesítés mentése
            _context.SolidManureDailies.Add(new SolidManureDaily
            {
                Date = date,
                TotalNet = totalNet,
                Cow = result.Cow,
                CalfMilk = result.CalfMilk,
                Calf3_6 = result.Calf3_6,
                Young6_9 = result.Young6_9,
                Young9_12 = result.Young9_12,
                PregnantHeifer = result.PregnantHeifer,

                VoucherIn = _voucher.FormatVoucher(voucherIn),

                // 🔥 több kiadás → stringként
                VoucherOut = string.Join(", ", expenseMap.Values)
            });

            await _context.SaveChangesAsync();

            return new
            {
                totalNet,
                result,
                income = _voucher.FormatVoucher(voucherIn),
                expenses = expenseMap.Values.ToList(),
                destinations = grouped
            };
        }
    }
}
